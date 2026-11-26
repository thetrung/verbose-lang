' VerBose Self-Hosting Compiler (verbose) - Generics Monomorphization Pass
' Transforms parametric Structure, Function, Sub, and Operator templates (Of T, ...)
' into concrete zero-overhead monomorphized AST definitions prior to Type Analysis and LLVM IR Codegen.

Include "constants.vb"
Include "parser.vb"

Public Structure GmType
  Dim kind, innerKind, arrLen, namePos, nameLen As Integer
End Structure

Structure GmStructTmpl
  Dim nodeId, namePos, nameLen, genStart, genCount, isPrivate, modId As Integer
End Structure

Structure GmFuncTmpl
  Dim nodeId, namePos, nameLen, genStart, genCount, isPrivate, isAsync, modId As Integer
End Structure

Structure GmOpTmpl
  Dim nodeId, opTok, genStart, genCount, isPrivate, modId As Integer
End Structure

Structure GmStructSpec
  Dim tmplIdx, argStart, argCount, mangledPos, mangledLen, defNodeId As Integer
End Structure

Structure GmFuncSpec
  Dim tmplIdx, argStart, argCount, mangledPos, mangledLen, defNodeId As Integer
End Structure

Structure GmOpSpec
  Dim tmplIdx, argStart, argCount, defNodeId As Integer
End Structure

Structure GmEnumInfo
  Dim namePos, nameLen, isPrivate, modId As Integer
End Structure

Structure GmConcreteStruct
  Dim namePos, nameLen, fieldStart, fieldCount, isPrivate, modId, nodeId As Integer
End Structure

Structure GmFieldInfo
  Dim namePos, nameLen As Integer, ty As GmType
End Structure

Structure GmGlobalInfo
  Dim namePos, nameLen, isPrivate, modId As Integer, ty As GmType
End Structure

Structure GmFuncInfo
  Dim namePos, nameLen, isPrivate, isAsync, modId As Integer, retTy As GmType
End Structure

Structure GmOpInfo
  Dim opTok, isPrivate, modId, paramCount As Integer
  Dim p0, p1, retTy As GmType
End Structure

Structure GmLocalSym
  Dim namePos, nameLen As Integer, ty As GmType
End Structure

Structure GmSubstEntry
  Dim namePos, nameLen As Integer, ty As GmType
End Structure

Private Static gm_structTmpls() As GmStructTmpl, gm_funcTmpls() As GmFuncTmpl, gm_opTmpls() As GmOpTmpl
Private Static gm_structSpecs() As GmStructSpec, gm_funcSpecs() As GmFuncSpec, gm_opSpecs() As GmOpSpec
Private Static gm_specArgs() As GmType
Private Static gm_enums() As GmEnumInfo, gm_cStructs() As GmConcreteStruct, gm_cFields() As GmFieldInfo
Private Static gm_globals() As GmGlobalInfo, gm_cFuncs() As GmFuncInfo, gm_cOps() As GmOpInfo
Private Static gm_locals() As GmLocalSym, gm_subst() As GmSubstEntry
Private Static gm_withTys(64) As GmType
Private Static gm_kidStack() As Integer, gm_newDefs() As Integer

Private Static gm_structTmplCount, gm_funcTmplCount, gm_opTmplCount As Integer = 0
Private Static gm_structSpecCount, gm_funcSpecCount, gm_opSpecCount, gm_specArgCount As Integer = 0
Private Static gm_enumCount, gm_cStructCount, gm_cFieldCount, gm_globalCount, gm_cFuncCount, gm_cOpCount As Integer = 0
Private Static gm_localBase, gm_localCount, gm_substBase, gm_substCount, gm_withBase, gm_withDepth As Integer = 0
Private Static gm_kidSp, gm_newDefCount, gm_depth, gm_curModId, gm_errCount As Integer = 0
Private Static gm_altModId As Integer = -1

' =============================================================================
' Helpers: Printing, Types & Name Mangling
' =============================================================================

Private Sub GmPrintSlice(pos, slen As Integer)
  For i = 0 To slen - 1 Do
    Dim ch As Integer = g_srcPool(pos + i)
    Printf("%c", ch)
  End For
End Sub

Private Function GmMakePrim(k As Integer) As GmType
  Dim t As GmType With ( kind = k, innerKind = TyNothing, arrLen = 0, namePos = 0, nameLen = 0 )
  Return t
End Function

Private Function GmTypesEqual(a As GmType, b As GmType) As Boolean
  If (a.kind <> b.kind) Or (a.arrLen <> b.arrLen) Then
    Return False
  End If
  If (a.kind = TyStruct) Or (a.kind = TyEnum) Then
    Return SlicesEqual(a.namePos, a.nameLen, b.namePos, b.nameLen)
  End If
  If a.kind = TyTask Then
    If a.innerKind <> b.innerKind Then
      Return False
    End If
    If (a.innerKind = TyStruct) Or (a.innerKind = TyEnum) Then
      Return SlicesEqual(a.namePos, a.nameLen, b.namePos, b.nameLen)
    End If
    Return True
  End If
  Return True
End Function

Private Function GmIsIntType(t As GmType) As Boolean
  If t.arrLen <> 0 Then
    Return False
  End If
  Return (t.kind = TyByte) Or (t.kind = TyShort) Or (t.kind = TyInt) Or (t.kind = TyLong) Or (t.kind = TyEnum)
End Function

Private Function GmIsFloatType(t As GmType) As Boolean
  If t.arrLen <> 0 Then
    Return False
  End If
  Return (t.kind = TySingle) Or (t.kind = TyDouble)
End Function

Private Function GmIsIntLitNode(id As Integer) As Boolean
  If id < 0 Then
    Return False
  End If
  Dim k As Integer = g_nodes(id).kind
  If k = NdIntLit Then
    Return True
  End If
  If (k = NdNeg) And (g_nodes(id).left >= 0) Then
    Return g_nodes(g_nodes(id).left).kind = NdIntLit
  End If
  Return False
End Function

Private Function GmIsFloatLitNode(id As Integer) As Boolean
  If id < 0 Then
    Return False
  End If
  Dim k As Integer = g_nodes(id).kind
  If k = NdFloatLit Then
    Return True
  End If
  If (k = NdNeg) And (g_nodes(id).left >= 0) Then
    Return g_nodes(g_nodes(id).left).kind = NdFloatLit
  End If
  Return False
End Function

Private Sub GmAppendStr(lit As Pointer, litLen As Integer)
  For i = 0 To litLen - 1 Do
    If g_srcPoolLen < (g_srcPoolCap - 1) Then
      g_srcPool(g_srcPoolLen) = lit(i)
      g_srcPoolLen = g_srcPoolLen + 1
    End If
  End For
End Sub

Private Sub GmAppendSlice(pos, slen As Integer)
  For i = 0 To slen - 1 Do
    If g_srcPoolLen < (g_srcPoolCap - 1) Then
      g_srcPool(g_srcPoolLen) = g_srcPool(pos + i)
      g_srcPoolLen = g_srcPoolLen + 1
    End If
  End For
End Sub

Private Sub GmAppendTypeMangle(t As GmType)
  Match t.kind With
    Case TyNothing Then GmAppendStr("Nothing", 7)
    Case TyInt Then GmAppendStr("Integer", 7)
    Case TyByte Then GmAppendStr("Byte", 4)
    Case TyShort Then GmAppendStr("Short", 5)
    Case TyLong Then GmAppendStr("Long", 4)
    Case TySingle Then GmAppendStr("Single", 6)
    Case TyDouble Then GmAppendStr("Double", 6)
    Case TyBoolean Then GmAppendStr("Boolean", 7)
    Case TyPointer Then GmAppendStr("Pointer", 7)
    Case TyArena Then GmAppendStr("Arena", 5)
    Case TyStruct, TyEnum Then GmAppendSlice(t.namePos, t.nameLen)
    Case TyTask Then
      If t.innerKind = TyNothing Then
        GmAppendStr("Task", 4)
      Else
        GmAppendStr("Task_", 5)
        Dim innerT As GmType With ( kind = t.innerKind, innerKind = TyNothing, arrLen = 0, namePos = t.namePos, nameLen = t.nameLen )
        GmAppendTypeMangle(innerT)
      End If
    Case _ Then GmAppendStr("Unknown", 7)
  End Match
End Sub

Private Sub GmMangleName(basePos, baseLen As Integer, argTys() As GmType, argCnt As Integer, ByRef outPos, outLen As Integer)
  outPos = g_srcPoolLen
  GmAppendSlice(basePos, baseLen)
  GmAppendStr("__Of_", 5)
  For i = 0 To argCnt - 1 Do
    If i > 0 Then
      GmAppendStr("_And_", 5)
    End If
    GmAppendTypeMangle(argTys(i))
  End For
  GmAppendStr("_End", 4)
  outLen = g_srcPoolLen - outPos
End Sub

' =============================================================================
' Symbol & Template Lookups
' =============================================================================

Private Function GmLookupSubst(pos, slen As Integer, ByRef outTy As GmType) As Boolean
  Dim i As Integer = gm_substCount - 1
  While i >= gm_substBase Do
    If SlicesEqual(gm_subst(i).namePos, gm_subst(i).nameLen, pos, slen) Then
      outTy = gm_subst(i).ty
      Return True
    End If
    i = i - 1
  End While
  Return False
End Function

Private Function GmFindStructTmpl(pos, slen As Integer) As Integer
  For i = 0 To gm_structTmplCount - 1 Do
    If (gm_structTmpls(i).isPrivate = 1) And ((gm_structTmpls(i).modId = gm_curModId) Or ((gm_altModId >= 0) And (gm_structTmpls(i).modId = gm_altModId))) And SlicesEqual(gm_structTmpls(i).namePos, gm_structTmpls(i).nameLen, pos, slen) Then
      Return i
    End If
  End For
  For i = 0 To gm_structTmplCount - 1 Do
    If (gm_structTmpls(i).isPrivate = 0) And SlicesEqual(gm_structTmpls(i).namePos, gm_structTmpls(i).nameLen, pos, slen) Then
      Return i
    End If
  End For
  Return -1
End Function

Private Function GmHasPrivateStructTmplOther(pos, slen As Integer) As Boolean
  For i = 0 To gm_structTmplCount - 1 Do
    If (gm_structTmpls(i).isPrivate = 1) And (gm_structTmpls(i).modId <> gm_curModId) And SlicesEqual(gm_structTmpls(i).namePos, gm_structTmpls(i).nameLen, pos, slen) Then
      Return True
    End If
  End For
  Return False
End Function

Private Function GmFindFuncTmpl(pos, slen As Integer) As Integer
  For i = 0 To gm_funcTmplCount - 1 Do
    If (gm_funcTmpls(i).isPrivate = 1) And (gm_funcTmpls(i).modId = gm_curModId) And SlicesEqual(gm_funcTmpls(i).namePos, gm_funcTmpls(i).nameLen, pos, slen) Then
      Return i
    End If
  End For
  For i = 0 To gm_funcTmplCount - 1 Do
    If (gm_funcTmpls(i).isPrivate = 0) And SlicesEqual(gm_funcTmpls(i).namePos, gm_funcTmpls(i).nameLen, pos, slen) Then
      Return i
    End If
  End For
  Return -1
End Function

Private Function GmHasPrivateFuncTmplOther(pos, slen As Integer) As Boolean
  For i = 0 To gm_funcTmplCount - 1 Do
    If (gm_funcTmpls(i).isPrivate = 1) And (gm_funcTmpls(i).modId <> gm_curModId) And SlicesEqual(gm_funcTmpls(i).namePos, gm_funcTmpls(i).nameLen, pos, slen) Then
      Return True
    End If
  End For
  Return False
End Function

Private Function GmFindEnum(pos, slen As Integer) As Integer
  For i = 0 To gm_enumCount - 1 Do
    If (gm_enums(i).isPrivate = 1) And ((gm_enums(i).modId = gm_curModId) Or ((gm_altModId >= 0) And (gm_enums(i).modId = gm_altModId))) And SlicesEqual(gm_enums(i).namePos, gm_enums(i).nameLen, pos, slen) Then
      Return i
    End If
  End For
  For i = 0 To gm_enumCount - 1 Do
    If (gm_enums(i).isPrivate = 0) And SlicesEqual(gm_enums(i).namePos, gm_enums(i).nameLen, pos, slen) Then
      Return i
    End If
  End For
  Return -1
End Function

Private Function GmFindConcreteStruct(pos, slen As Integer) As Integer
  For i = 0 To gm_cStructCount - 1 Do
    If (gm_cStructs(i).isPrivate = 1) And ((gm_cStructs(i).modId = gm_curModId) Or ((gm_altModId >= 0) And (gm_cStructs(i).modId = gm_altModId))) And SlicesEqual(gm_cStructs(i).namePos, gm_cStructs(i).nameLen, pos, slen) Then
      Return i
    End If
  End For
  For i = 0 To gm_cStructCount - 1 Do
    If (gm_cStructs(i).isPrivate = 0) And SlicesEqual(gm_cStructs(i).namePos, gm_cStructs(i).nameLen, pos, slen) Then
      Return i
    End If
  End For
  Return -1
End Function

Private Function GmFindStructField(sIdx, pos, slen As Integer, ByRef outTy As GmType) As Integer
  Dim st = gm_cStructs(sIdx).fieldStart, cnt = gm_cStructs(sIdx).fieldCount As Integer
  For i = 0 To cnt - 1 Do
    If SlicesEqual(gm_cFields(st + i).namePos, gm_cFields(st + i).nameLen, pos, slen) Then
      outTy = gm_cFields(st + i).ty
      Return i
    End If
  End For
  Return -1
End Function

Private Function GmFindGlobal(pos, slen As Integer) As Integer
  For i = 0 To gm_globalCount - 1 Do
    If (gm_globals(i).isPrivate = 1) And (gm_globals(i).modId = gm_curModId) And SlicesEqual(gm_globals(i).namePos, gm_globals(i).nameLen, pos, slen) Then
      Return i
    End If
  End For
  For i = 0 To gm_globalCount - 1 Do
    If (gm_globals(i).isPrivate = 0) And SlicesEqual(gm_globals(i).namePos, gm_globals(i).nameLen, pos, slen) Then
      Return i
    End If
  End For
  Return -1
End Function

Private Function GmFindConcreteFunc(pos, slen As Integer) As Integer
  For i = 0 To gm_cFuncCount - 1 Do
    If (gm_cFuncs(i).isPrivate = 1) And (gm_cFuncs(i).modId = gm_curModId) And SlicesEqual(gm_cFuncs(i).namePos, gm_cFuncs(i).nameLen, pos, slen) Then
      Return i
    End If
  End For
  For i = 0 To gm_cFuncCount - 1 Do
    If (gm_cFuncs(i).isPrivate = 0) And SlicesEqual(gm_cFuncs(i).namePos, gm_cFuncs(i).nameLen, pos, slen) Then
      Return i
    End If
  End For
  Return -1
End Function

Private Function GmFindLocal(pos, slen As Integer) As Integer
  Dim i As Integer = gm_localCount - 1
  While i >= gm_localBase Do
    If SlicesEqual(gm_locals(i).namePos, gm_locals(i).nameLen, pos, slen) Then
      Return i
    End If
    i = i - 1
  End While
  Return -1
End Function

Private Function GmLookupVar(pos, slen As Integer, ByRef outTy As GmType) As Boolean
  Dim li As Integer = GmFindLocal(pos, slen)
  If li >= 0 Then
    outTy = gm_locals(li).ty
    Return True
  End If
  Dim gi As Integer = GmFindGlobal(pos, slen)
  If gi >= 0 Then
    outTy = gm_globals(gi).ty
    Return True
  End If
  Return False
End Function

Private Function GmFindStructSpec(tmplIdx As Integer, argTys() As GmType, argCnt As Integer) As Integer
  For i = 0 To gm_structSpecCount - 1 Do
    If (gm_structSpecs(i).tmplIdx = tmplIdx) And (gm_structSpecs(i).argCount = argCnt) Then
      Dim ok As Boolean = True
      For k = 0 To argCnt - 1 Do
        If Not GmTypesEqual(gm_specArgs(gm_structSpecs(i).argStart + k), argTys(k)) Then
          ok = False
        End If
      End For
      If ok Then
        Return i
      End If
    End If
  End For
  Return -1
End Function

Private Function GmFindStructSpecByName(pos, slen As Integer) As Integer
  For i = 0 To gm_structSpecCount - 1 Do
    If SlicesEqual(gm_structSpecs(i).mangledPos, gm_structSpecs(i).mangledLen, pos, slen) Then
      Return i
    End If
  End For
  Return -1
End Function

Private Function GmFindFuncSpec(tmplIdx As Integer, argTys() As GmType, argCnt As Integer) As Integer
  For i = 0 To gm_funcSpecCount - 1 Do
    If (gm_funcSpecs(i).tmplIdx = tmplIdx) And (gm_funcSpecs(i).argCount = argCnt) Then
      Dim ok As Boolean = True
      For k = 0 To argCnt - 1 Do
        If Not GmTypesEqual(gm_specArgs(gm_funcSpecs(i).argStart + k), argTys(k)) Then
          ok = False
        End If
      End For
      If ok Then
        Return i
      End If
    End If
  End For
  Return -1
End Function

Private Function GmFindOpSpec(tmplIdx As Integer, argTys() As GmType, argCnt As Integer) As Integer
  For i = 0 To gm_opSpecCount - 1 Do
    If (gm_opSpecs(i).tmplIdx = tmplIdx) And (gm_opSpecs(i).argCount = argCnt) Then
      Dim ok As Boolean = True
      For k = 0 To argCnt - 1 Do
        If Not GmTypesEqual(gm_specArgs(gm_opSpecs(i).argStart + k), argTys(k)) Then
          ok = False
        End If
      End For
      If ok Then
        Return i
      End If
    End If
  End For
  Return -1
End Function

Private Function GmFindConcreteBinOp(opTok As Integer, t1 As GmType, e1 As Integer, t2 As GmType, e2 As Integer) As Integer
  For i = 0 To gm_cOpCount - 1 Do
    If (gm_cOps(i).opTok = opTok) And (gm_cOps(i).paramCount = 2) Then
      If (gm_cOps(i).isPrivate = 0) Or (gm_cOps(i).modId = gm_curModId) Then
        Dim m1 As Boolean = GmTypesEqual(gm_cOps(i).p0, t1) Or (GmIsIntType(gm_cOps(i).p0) And GmIsIntLitNode(e1)) Or (GmIsFloatType(gm_cOps(i).p0) And GmIsFloatLitNode(e1))
        Dim m2 As Boolean = GmTypesEqual(gm_cOps(i).p1, t2) Or (GmIsIntType(gm_cOps(i).p1) And GmIsIntLitNode(e2)) Or (GmIsFloatType(gm_cOps(i).p1) And GmIsFloatLitNode(e2))
        If m1 And m2 Then
          Return i
        End If
      End If
    End If
  End For
  Return -1
End Function

Private Function GmFindConcreteUnaryOp(opTok As Integer, t1 As GmType) As Integer
  For i = 0 To gm_cOpCount - 1 Do
    If (gm_cOps(i).opTok = opTok) And (gm_cOps(i).paramCount = 1) Then
      If (gm_cOps(i).isPrivate = 0) Or (gm_cOps(i).modId = gm_curModId) Then
        If GmTypesEqual(gm_cOps(i).p0, t1) Then
          Return i
        End If
      End If
    End If
  End For
  Return -1
End Function

Private Function GmIsBuiltinName(pos, slen As Integer) As Boolean
  If SliceEqualsStr(pos, slen, "IsNull", 6) Or SliceEqualsStr(pos, slen, "Len", 3) Or SliceEqualsStr(pos, slen, "Printf", 6) Then
    Return True
  End If
  If SliceEqualsStr(pos, slen, "ArenaCreate", 11) Or SliceEqualsStr(pos, slen, "ArenaFromBuffer", 15) Or SliceEqualsStr(pos, slen, "ArenaAllocBytes", 15) Or SliceEqualsStr(pos, slen, "ArenaReset", 10) Or SliceEqualsStr(pos, slen, "ArenaDestroy", 12) Or SliceEqualsStr(pos, slen, "ArenaMark", 9) Or SliceEqualsStr(pos, slen, "ArenaRewind", 11) Or SliceEqualsStr(pos, slen, "ArenaUsed", 9) Or SliceEqualsStr(pos, slen, "ArenaRemaining", 14) Or SliceEqualsStr(pos, slen, "ArenaCapacity", 13) Then
    Return True
  End If
  If SliceEqualsStr(pos, slen, "HeapAllocBytes", 14) Or SliceEqualsStr(pos, slen, "HeapFree", 8) Or SliceEqualsStr(pos, slen, "Malloc", 6) Or SliceEqualsStr(pos, slen, "Calloc", 6) Or SliceEqualsStr(pos, slen, "Realloc", 7) Or SliceEqualsStr(pos, slen, "Free", 4) Or SliceEqualsStr(pos, slen, "Fopen", 5) Or SliceEqualsStr(pos, slen, "Getc", 4) Or SliceEqualsStr(pos, slen, "Putc", 4) Then
    Return True
  End If
  Return False
End Function

' =============================================================================
' Type Resolution & Substitution
' =============================================================================

Private Function GmResolveTypeSpec(rawKind, rawInner, arrLen, nPos, nLen, taStart, taCount As Integer) As GmType
  Dim res As GmType With ( kind = rawKind, innerKind = rawInner, arrLen = arrLen, namePos = nPos, nameLen = nLen )
  If rawKind = TyTask Then
    If rawInner = TyStruct Then
      Dim innerRes As GmType = GmResolveTypeSpec(TyStruct, TyNothing, 0, nPos, nLen, taStart, taCount)
      If innerRes.kind = TyTask Then
        Printf(MSG_GM_NESTED_TASK)
        gm_errCount = gm_errCount + 1
        Return res
      End If
      res.innerKind = innerRes.kind
      res.namePos = innerRes.namePos
      res.nameLen = innerRes.nameLen
    End If
    Return res
  End If

  If rawKind = TyStruct Then
    If taCount = 0 Then
      Dim subTy As GmType
      If GmLookupSubst(nPos, nLen, ByRef subTy) Then
        subTy.arrLen = arrLen
        Return subTy
      End If
      If GmFindEnum(nPos, nLen) >= 0 Then
        res.kind = TyEnum
        Return res
      End If
      If (GmFindStructTmpl(nPos, nLen) >= 0) And (GmFindConcreteStruct(nPos, nLen) < 0) Then
        Printf("Generic Error: Generic structure '")
        GmPrintSlice(nPos, nLen)
        Printf("' requires type arguments (Of ...)\n")
        gm_errCount = gm_errCount + 1
      End If
      Return res
    Else
      Dim argTys(16) As GmType
      For i = 0 To taCount - 1 Do
        Dim tr As Integer = g_kids(taStart + i)
        argTys(i) = GmResolveTypeSpec(g_nodes(tr).tyKind, g_nodes(tr).tyInnerKind, 0, g_nodes(tr).tyNamePos, g_nodes(tr).tyNameLen, g_nodes(tr).tyArgStart, g_nodes(tr).tyArgCount)
      End For
      Dim tIdx As Integer = GmFindStructTmpl(nPos, nLen)
      If tIdx < 0 Then
        If GmHasPrivateStructTmplOther(nPos, nLen) Then
          Printf("Generic Error: Cannot instantiate Private generic structure '")
          GmPrintSlice(nPos, nLen)
          Printf("' from included module\n")
        Else
          Printf("Generic Error: Unknown generic structure '")
          GmPrintSlice(nPos, nLen)
          Printf("'\n")
        End If
        gm_errCount = gm_errCount + 1
        Return res
      End If
      If gm_structTmpls(tIdx).genCount <> taCount Then
        Printf("Generic Error: Structure '")
        GmPrintSlice(nPos, nLen)
        Printf("' expects %d type argument(s), got %d\n", gm_structTmpls(tIdx).genCount, taCount)
        gm_errCount = gm_errCount + 1
        Return res
      End If
      Dim mPos = 0, mLen = 0 As Integer
      GmInstantiateStruct(tIdx, argTys, taCount, gm_curModId, ByRef mPos, ByRef mLen)
      res.kind = TyStruct
      res.innerKind = TyNothing
      res.namePos = mPos
      res.nameLen = mLen
      Return res
    End If
  End If

  Return res
End Function

Private Function GmApplyResolvedType(nodeId As Integer) As GmType
  Dim r As GmType = GmResolveTypeSpec(g_nodes(nodeId).tyKind, g_nodes(nodeId).tyInnerKind, g_nodes(nodeId).tyArrLen, g_nodes(nodeId).tyNamePos, g_nodes(nodeId).tyNameLen, g_nodes(nodeId).tyArgStart, g_nodes(nodeId).tyArgCount)
  With g_nodes(nodeId)
    .tyKind = r.kind
    .tyInnerKind = r.innerKind
    .tyNamePos = r.namePos
    .tyNameLen = r.nameLen
    .tyArgStart = 0
    .tyArgCount = 0
  End With
  If g_nodes(nodeId).tyKind = TyEnum Then
    g_nodes(nodeId).tyKind = TyStruct
  End If
  Return r
End Function

' =============================================================================
' Structural Type Pattern Unification (Call-Site & Operator Type Inference)
' =============================================================================

Private Function GmFindGenParamIdx(genStart, genCount, pos, slen As Integer) As Integer
  For i = 0 To genCount - 1 Do
    Dim tp As Integer = g_kids(genStart + i)
    If SlicesEqual(g_nodes(tp).tyNamePos, g_nodes(tp).tyNameLen, pos, slen) Then
      Return i
    End If
  End For
  Return -1
End Function

Private Sub GmUnifyType(genStart, genCount, patKind, patInner, patNPos, patNLen, patTaStart, patTaCount, patArrLen As Integer, actTy As GmType, actExprNode As Integer, bound() As Integer, boundTy() As GmType)
  If (patArrLen <> 0) And (actTy.arrLen = 0) Then
    If (patArrLen <> -1) Or (actTy.kind <> TyPointer) Then
      Return
    End If
  End If
  If (patArrLen = 0) And (actTy.arrLen <> 0) Then
    Return
  End If

  If patKind = TyTask Then
    If (actTy.kind = TyTask) And (patInner = TyStruct) Then
      Dim actInner As GmType With ( kind = actTy.innerKind, innerKind = TyNothing, arrLen = 0, namePos = actTy.namePos, nameLen = actTy.nameLen )
      GmUnifyType(genStart, genCount, TyStruct, TyNothing, patNPos, patNLen, patTaStart, patTaCount, 0, actInner, -1, bound, boundTy)
    End If
    Return
  End If

  If patKind <> TyStruct Then
    Return
  End If

  If patTaCount = 0 Then
    Dim gpIdx As Integer = GmFindGenParamIdx(genStart, genCount, patNPos, patNLen)
    If gpIdx >= 0 Then
      Dim elemAct As GmType = actTy
      elemAct.arrLen = 0
      Dim prio As Integer = 2
      If (actExprNode >= 0) And (GmIsIntLitNode(actExprNode) Or GmIsFloatLitNode(actExprNode)) Then
        prio = 1
      End If
      If bound(gpIdx) = 0 Then
        bound(gpIdx) = prio
        boundTy(gpIdx) = elemAct
      Else
        If (bound(gpIdx) = 1) And (prio = 2) Then
          If (GmIsIntType(boundTy(gpIdx)) And GmIsIntType(elemAct)) Or (GmIsFloatType(boundTy(gpIdx)) And GmIsFloatType(elemAct)) Or GmTypesEqual(boundTy(gpIdx), elemAct) Then
            bound(gpIdx) = 2
            boundTy(gpIdx) = elemAct
          End If
        End If
      End If
    End If
    Return
  End If

  ' Generic struct pattern: S(Of P1, P2, ...) matched against concrete monomorphized S__Of_A1_And_A2_End
  If actTy.kind = TyStruct Then
    Dim sSpecIdx As Integer = GmFindStructSpecByName(actTy.namePos, actTy.nameLen)
    Dim tIdx As Integer = GmFindStructTmpl(patNPos, patNLen)
    If (sSpecIdx >= 0) And (tIdx >= 0) And (gm_structSpecs(sSpecIdx).tmplIdx = tIdx) And (gm_structSpecs(sSpecIdx).argCount = patTaCount) Then
      For i = 0 To patTaCount - 1 Do
        Dim tr As Integer = g_kids(patTaStart + i)
        Dim subAct As GmType = gm_specArgs(gm_structSpecs(sSpecIdx).argStart + i)
        GmUnifyType(genStart, genCount, g_nodes(tr).tyKind, g_nodes(tr).tyInnerKind, g_nodes(tr).tyNamePos, g_nodes(tr).tyNameLen, g_nodes(tr).tyArgStart, g_nodes(tr).tyArgCount, 0, subAct, -1, bound, boundTy)
      End For
    End If
  End If
End Sub

' =============================================================================
' Node Cloning Helper
' =============================================================================

Private Function GmCopyNode(srcId As Integer) As Integer
  Dim dstId As Integer = g_nodeCount
  g_nodes(dstId) = g_nodes(srcId)
  g_nodeCount = g_nodeCount + 1
  Return dstId
End Function

' =============================================================================
' Generic Instantiation: Structures, Functions/Subs, Operators
' =============================================================================

Private Sub GmInstantiateStruct(tmplIdx As Integer, argTys() As GmType, argCnt, callerModId As Integer, ByRef outPos, outLen As Integer)
  Dim existing As Integer = GmFindStructSpec(tmplIdx, argTys, argCnt)
  If existing >= 0 Then
    outPos = gm_structSpecs(existing).mangledPos
    outLen = gm_structSpecs(existing).mangledLen
    Return
  End If
  If gm_depth >= 32 Then
    Printf(MSG_GM_MAX_DEPTH)
    gm_errCount = gm_errCount + 1
    outPos = gm_structTmpls(tmplIdx).namePos
    outLen = gm_structTmpls(tmplIdx).nameLen
    Return
  End If
  If gm_structSpecCount >= 512 Then
    Printf(MSG_GM_MAX_SPECS)
    gm_errCount = gm_errCount + 1
    outPos = gm_structTmpls(tmplIdx).namePos
    outLen = gm_structTmpls(tmplIdx).nameLen
    Return
  End If

  Dim tmpl As GmStructTmpl = gm_structTmpls(tmplIdx)
  Dim mPos = 0, mLen = 0 As Integer
  GmMangleName(tmpl.namePos, tmpl.nameLen, argTys, argCnt, ByRef mPos, ByRef mLen)
  outPos = mPos
  outLen = mLen

  ' Pre-register specialization before cloning fields (supports self-reference via Pointer)
  Dim aStart As Integer = gm_specArgCount
  For i = 0 To argCnt - 1 Do
    gm_specArgs(gm_specArgCount) = argTys(i)
    gm_specArgCount = gm_specArgCount + 1
  End For
  Dim specIdx As Integer = gm_structSpecCount
  gm_structSpecs(specIdx) = GmStructSpec With (
    tmplIdx = tmplIdx,
    argStart = aStart,
    argCount = argCnt,
    mangledPos = mPos,
    mangledLen = mLen,
    defNodeId = -1
  )
  gm_structSpecCount = gm_structSpecCount + 1

  ' Pre-register concrete struct header
  Dim cStructIdx As Integer = gm_cStructCount
  gm_cStructs(cStructIdx) = GmConcreteStruct With (
    namePos = mPos,
    nameLen = mLen,
    fieldStart = 0,
    fieldCount = 0,
    isPrivate = tmpl.isPrivate,
    modId = tmpl.modId,
    nodeId = -1
  )
  gm_cStructCount = gm_cStructCount + 1

  ' Push isolated substitution frame
  Dim savedSubstBase = gm_substBase, savedSubstCount = gm_substCount As Integer
  Dim savedMod = gm_curModId, savedAlt = gm_altModId As Integer
  gm_substBase = gm_substCount
  For i = 0 To argCnt - 1 Do
    Dim tp As Integer = g_kids(tmpl.genStart + i)
    gm_subst(gm_substCount) = GmSubstEntry With ( namePos = g_nodes(tp).tyNamePos, nameLen = g_nodes(tp).tyNameLen, ty = argTys(i) )
    gm_substCount = gm_substCount + 1
  End For
  gm_curModId = tmpl.modId
  gm_altModId = callerModId
  gm_depth = gm_depth + 1

  Dim srcDef As Integer = tmpl.nodeId
  Dim fc As Integer = g_nodes(srcDef).childCount
  Dim savedSp As Integer = gm_kidSp
  For i = 0 To fc - 1 Do
    Dim srcF As Integer = g_kids(g_nodes(srcDef).firstChild + i)
    Dim newF As Integer = GmCopyNode(srcF)
    GmApplyResolvedType(newF)
    gm_kidStack(gm_kidSp) = newF
    gm_kidSp = gm_kidSp + 1
  End For

  Dim newDef As Integer = GmCopyNode(srcDef)
  With g_nodes(newDef)
    .intVal = mPos
    .lenVal = mLen
    .genStart = 0
    .genCount = 0
    .third = callerModId
    .firstChild = g_kidCount
    .childCount = fc
  End With
  Dim fStart As Integer = gm_cFieldCount
  For i = 0 To fc - 1 Do
    Dim fnId As Integer = gm_kidStack(savedSp + i)
    PushKid(fnId)
    Dim fty As GmType With (
      kind = g_nodes(fnId).tyKind,
      innerKind = g_nodes(fnId).tyInnerKind,
      arrLen = g_nodes(fnId).tyArrLen,
      namePos = g_nodes(fnId).tyNamePos,
      nameLen = g_nodes(fnId).tyNameLen
    )
    If (fty.kind = TyStruct) And (GmFindEnum(fty.namePos, fty.nameLen) >= 0) Then
      fty.kind = TyEnum
    End If
    gm_cFields(gm_cFieldCount) = GmFieldInfo With ( namePos = g_nodes(fnId).intVal, nameLen = g_nodes(fnId).lenVal, ty = fty )
    gm_cFieldCount = gm_cFieldCount + 1
  End For
  gm_kidSp = savedSp

  gm_cStructs(cStructIdx).fieldStart = fStart
  gm_cStructs(cStructIdx).fieldCount = fc
  gm_cStructs(cStructIdx).nodeId = newDef
  gm_structSpecs(specIdx).defNodeId = newDef

  gm_newDefs(gm_newDefCount) = newDef
  gm_newDefCount = gm_newDefCount + 1

  gm_depth = gm_depth - 1
  gm_curModId = savedMod
  gm_altModId = savedAlt
  gm_substCount = savedSubstCount
  gm_substBase = savedSubstBase
End Sub

Private Sub GmInstantiateFunc(tmplIdx As Integer, argTys() As GmType, argCnt, callerModId As Integer, ByRef outPos, outLen As Integer)
  Dim existing As Integer = GmFindFuncSpec(tmplIdx, argTys, argCnt)
  If existing >= 0 Then
    outPos = gm_funcSpecs(existing).mangledPos
    outLen = gm_funcSpecs(existing).mangledLen
    Return
  End If
  If gm_depth >= 32 Then
    Printf(MSG_GM_MAX_DEPTH)
    gm_errCount = gm_errCount + 1
    outPos = gm_funcTmpls(tmplIdx).namePos
    outLen = gm_funcTmpls(tmplIdx).nameLen
    Return
  End If
  If gm_funcSpecCount >= 1024 Then
    Printf(MSG_GM_MAX_SPECS)
    gm_errCount = gm_errCount + 1
    outPos = gm_funcTmpls(tmplIdx).namePos
    outLen = gm_funcTmpls(tmplIdx).nameLen
    Return
  End If

  Dim tmpl As GmFuncTmpl = gm_funcTmpls(tmplIdx)
  Dim mPos = 0, mLen = 0 As Integer
  GmMangleName(tmpl.namePos, tmpl.nameLen, argTys, argCnt, ByRef mPos, ByRef mLen)
  outPos = mPos
  outLen = mLen

  ' Pre-register specialization before cloning body (handles recursive generic calls!)
  Dim aStart As Integer = gm_specArgCount
  For i = 0 To argCnt - 1 Do
    gm_specArgs(gm_specArgCount) = argTys(i)
    gm_specArgCount = gm_specArgCount + 1
  End For
  Dim specIdx As Integer = gm_funcSpecCount
  gm_funcSpecs(specIdx) = GmFuncSpec With (
    tmplIdx = tmplIdx,
    argStart = aStart,
    argCount = argCnt,
    mangledPos = mPos,
    mangledLen = mLen,
    defNodeId = -1
  )
  gm_funcSpecCount = gm_funcSpecCount + 1

  ' Push isolated substitution, local, and With scopes
  Dim savedSubstBase = gm_substBase, savedSubstCount = gm_substCount As Integer
  Dim savedLocalBase = gm_localBase, savedLocalCount = gm_localCount As Integer
  Dim savedWithBase = gm_withBase, savedWithDepth = gm_withDepth As Integer
  Dim savedMod = gm_curModId, savedAlt = gm_altModId As Integer

  gm_substBase = gm_substCount
  For i = 0 To argCnt - 1 Do
    Dim tp As Integer = g_kids(tmpl.genStart + i)
    gm_subst(gm_substCount) = GmSubstEntry With ( namePos = g_nodes(tp).tyNamePos, nameLen = g_nodes(tp).tyNameLen, ty = argTys(i) )
    gm_substCount = gm_substCount + 1
  End For
  gm_localBase = gm_localCount
  gm_withBase = gm_withDepth
  gm_curModId = tmpl.modId
  gm_altModId = callerModId
  gm_depth = gm_depth + 1

  Dim srcDef As Integer = tmpl.nodeId
  Dim newDef As Integer = GmCopyNode(srcDef)
  Dim retTy As GmType = GmApplyResolvedType(newDef)
  Dim callRetTy As GmType = retTy
  If tmpl.isAsync = 1 Then
    callRetTy = GmType With ( kind = TyTask, innerKind = retTy.kind, arrLen = 0, namePos = retTy.namePos, nameLen = retTy.nameLen )
  End If

  ' Pre-register concrete function signature so recursive calls infer return type
  gm_cFuncs(gm_cFuncCount) = GmFuncInfo With (
    namePos = mPos,
    nameLen = mLen,
    isPrivate = tmpl.isPrivate,
    isAsync = tmpl.isAsync,
    modId = tmpl.modId,
    retTy = callRetTy
  )
  gm_cFuncCount = gm_cFuncCount + 1

  ' Clone parameters and bind in local scope
  Dim pc As Integer = g_nodes(srcDef).extraCount
  Dim savedParamSp As Integer = gm_kidSp
  For i = 0 To pc - 1 Do
    Dim srcP As Integer = g_kids(g_nodes(srcDef).extraStart + i)
    Dim newP As Integer = GmCopyNode(srcP)
    Dim pTy As GmType = GmApplyResolvedType(newP)
    gm_locals(gm_localCount) = GmLocalSym With ( namePos = g_nodes(newP).intVal, nameLen = g_nodes(newP).lenVal, ty = pTy )
    gm_localCount = gm_localCount + 1
    gm_kidStack(gm_kidSp) = newP
    gm_kidSp = gm_kidSp + 1
  End For

  Dim pStartKid As Integer = g_kidCount
  For i = 0 To pc - 1 Do
    PushKid(gm_kidStack(savedParamSp + i))
  End For
  gm_kidSp = savedParamSp

  ' Clone body statements
  Dim bc As Integer = g_nodes(srcDef).childCount
  Dim savedBodySp As Integer = gm_kidSp
  For i = 0 To bc - 1 Do
    Dim srcS As Integer = g_kids(g_nodes(srcDef).firstChild + i)
    Dim newS As Integer = GmProcessStmt(srcS, 1)
    gm_kidStack(gm_kidSp) = newS
    gm_kidSp = gm_kidSp + 1
  End For

  Dim bStartKid As Integer = g_kidCount
  For i = 0 To bc - 1 Do
    PushKid(gm_kidStack(savedBodySp + i))
  End For
  gm_kidSp = savedBodySp

  With g_nodes(newDef)
    .intVal = mPos
    .lenVal = mLen
    .genStart = 0
    .genCount = 0
    .third = callerModId
    .extraStart = pStartKid
    .extraCount = pc
    .firstChild = bStartKid
    .childCount = bc
  End With
  gm_funcSpecs(specIdx).defNodeId = newDef
  gm_newDefs(gm_newDefCount) = newDef
  gm_newDefCount = gm_newDefCount + 1

  gm_depth = gm_depth - 1
  gm_curModId = savedMod
  gm_altModId = savedAlt
  gm_withDepth = savedWithDepth
  gm_withBase = savedWithBase
  gm_localCount = savedLocalCount
  gm_localBase = savedLocalBase
  gm_substCount = savedSubstCount
  gm_substBase = savedSubstBase
End Sub

Private Function GmInstantiateOp(tmplIdx As Integer, argTys() As GmType, argCnt, callerModId As Integer) As GmType
  Dim tmpl As GmOpTmpl = gm_opTmpls(tmplIdx)
  Dim existing As Integer = GmFindOpSpec(tmplIdx, argTys, argCnt)
  If existing >= 0 Then
    Dim defId As Integer = gm_opSpecs(existing).defNodeId
    If defId >= 0 Then
      Dim r As GmType With ( kind = g_nodes(defId).tyKind, innerKind = g_nodes(defId).tyInnerKind, arrLen = 0, namePos = g_nodes(defId).tyNamePos, nameLen = g_nodes(defId).tyNameLen )
      Return r
    End If
    Return GmMakePrim(TyInt)
  End If
  If gm_depth >= 32 Then
    Printf(MSG_GM_MAX_DEPTH)
    gm_errCount = gm_errCount + 1
    Return GmMakePrim(TyInt)
  End If
  If gm_opSpecCount >= 256 Then
    Printf(MSG_GM_MAX_SPECS)
    gm_errCount = gm_errCount + 1
    Return GmMakePrim(TyInt)
  End If

  Dim aStart As Integer = gm_specArgCount
  For i = 0 To argCnt - 1 Do
    gm_specArgs(gm_specArgCount) = argTys(i)
    gm_specArgCount = gm_specArgCount + 1
  End For
  Dim specIdx As Integer = gm_opSpecCount
  gm_opSpecs(specIdx) = GmOpSpec With ( tmplIdx = tmplIdx, argStart = aStart, argCount = argCnt, defNodeId = -1 )
  gm_opSpecCount = gm_opSpecCount + 1

  Dim savedSubstBase = gm_substBase, savedSubstCount = gm_substCount As Integer
  Dim savedLocalBase = gm_localBase, savedLocalCount = gm_localCount As Integer
  Dim savedWithBase = gm_withBase, savedWithDepth = gm_withDepth As Integer
  Dim savedMod = gm_curModId, savedAlt = gm_altModId As Integer

  gm_substBase = gm_substCount
  For i = 0 To argCnt - 1 Do
    Dim tp As Integer = g_kids(tmpl.genStart + i)
    gm_subst(gm_substCount) = GmSubstEntry With ( namePos = g_nodes(tp).tyNamePos, nameLen = g_nodes(tp).tyNameLen, ty = argTys(i) )
    gm_substCount = gm_substCount + 1
  End For
  gm_localBase = gm_localCount
  gm_withBase = gm_withDepth
  gm_curModId = tmpl.modId
  gm_altModId = callerModId
  gm_depth = gm_depth + 1

  Dim srcDef As Integer = tmpl.nodeId
  Dim newDef As Integer = GmCopyNode(srcDef)
  Dim retTy As GmType = GmApplyResolvedType(newDef)

  Dim pc As Integer = g_nodes(srcDef).extraCount
  Dim savedParamSp As Integer = gm_kidSp
  Dim p0Ty = GmMakePrim(TyNothing), p1Ty = GmMakePrim(TyNothing) As GmType
  For i = 0 To pc - 1 Do
    Dim srcP As Integer = g_kids(g_nodes(srcDef).extraStart + i)
    Dim newP As Integer = GmCopyNode(srcP)
    Dim pTy As GmType = GmApplyResolvedType(newP)
    If i = 0 Then
      p0Ty = pTy
    Else
      If i = 1 Then
        p1Ty = pTy
      End If
    End If
    gm_locals(gm_localCount) = GmLocalSym With ( namePos = g_nodes(newP).intVal, nameLen = g_nodes(newP).lenVal, ty = pTy )
    gm_localCount = gm_localCount + 1
    gm_kidStack(gm_kidSp) = newP
    gm_kidSp = gm_kidSp + 1
  End For

  Dim pStartKid As Integer = g_kidCount
  For i = 0 To pc - 1 Do
    PushKid(gm_kidStack(savedParamSp + i))
  End For
  gm_kidSp = savedParamSp

  ' Pre-register concrete operator for inference inside body
  gm_cOps(gm_cOpCount) = GmOpInfo With ( opTok = tmpl.opTok, isPrivate = tmpl.isPrivate, modId = tmpl.modId, paramCount = pc, p0 = p0Ty, p1 = p1Ty, retTy = retTy )
  gm_cOpCount = gm_cOpCount + 1
  gm_opSpecs(specIdx).defNodeId = newDef

  Dim bc As Integer = g_nodes(srcDef).childCount
  Dim savedBodySp As Integer = gm_kidSp
  For i = 0 To bc - 1 Do
    Dim srcS As Integer = g_kids(g_nodes(srcDef).firstChild + i)
    Dim newS As Integer = GmProcessStmt(srcS, 1)
    gm_kidStack(gm_kidSp) = newS
    gm_kidSp = gm_kidSp + 1
  End For

  Dim bStartKid As Integer = g_kidCount
  For i = 0 To bc - 1 Do
    PushKid(gm_kidStack(savedBodySp + i))
  End For
  gm_kidSp = savedBodySp

  With g_nodes(newDef)
    .genStart = 0
    .genCount = 0
    .third = callerModId
    .extraStart = pStartKid
    .extraCount = pc
    .firstChild = bStartKid
    .childCount = bc
  End With
  gm_newDefs(gm_newDefCount) = newDef
  gm_newDefCount = gm_newDefCount + 1

  gm_depth = gm_depth - 1
  gm_curModId = savedMod
  gm_altModId = savedAlt
  gm_withDepth = savedWithDepth
  gm_withBase = savedWithBase
  gm_localCount = savedLocalCount
  gm_localBase = savedLocalBase
  gm_substCount = savedSubstCount
  gm_substBase = savedSubstBase
  Return retTy
End Function

Private Function GmTryMatchGenericBinOp(opTok As Integer, t1 As GmType, e1 As Integer, t2 As GmType, e2 As Integer, ByRef outRet As GmType) As Boolean
  For i = 0 To gm_opTmplCount - 1 Do
    Dim tmpl As GmOpTmpl = gm_opTmpls(i)
    If (tmpl.opTok = opTok) And (g_nodes(tmpl.nodeId).extraCount = 2) Then
      If (tmpl.isPrivate = 0) Or (tmpl.modId = gm_curModId) Then
        Dim bound(16) As Integer
        Dim boundTy(16) As GmType
        For bi = 0 To tmpl.genCount - 1 Do
          bound(bi) = 0
        End For
        Dim p0 As Integer = g_kids(g_nodes(tmpl.nodeId).extraStart)
        Dim p1 As Integer = g_kids(g_nodes(tmpl.nodeId).extraStart + 1)
        GmUnifyType(tmpl.genStart, tmpl.genCount, g_nodes(p0).tyKind, g_nodes(p0).tyInnerKind, g_nodes(p0).tyNamePos, g_nodes(p0).tyNameLen, g_nodes(p0).tyArgStart, g_nodes(p0).tyArgCount, g_nodes(p0).tyArrLen, t1, e1, bound, boundTy)
        GmUnifyType(tmpl.genStart, tmpl.genCount, g_nodes(p1).tyKind, g_nodes(p1).tyInnerKind, g_nodes(p1).tyNamePos, g_nodes(p1).tyNameLen, g_nodes(p1).tyArgStart, g_nodes(p1).tyArgCount, g_nodes(p1).tyArrLen, t2, e2, bound, boundTy)
        Dim allBound As Boolean = True
        For bi = 0 To tmpl.genCount - 1 Do
          If bound(bi) = 0 Then
            allBound = False
          End If
        End For
        If allBound Then
          outRet = GmInstantiateOp(i, boundTy, tmpl.genCount, gm_curModId)
          Return True
        End If
      End If
    End If
  End For
  Return False
End Function

Private Function GmTryMatchGenericUnaryOp(opTok As Integer, t1 As GmType, e1 As Integer, ByRef outRet As GmType) As Boolean
  For i = 0 To gm_opTmplCount - 1 Do
    Dim tmpl As GmOpTmpl = gm_opTmpls(i)
    If (tmpl.opTok = opTok) And (g_nodes(tmpl.nodeId).extraCount = 1) Then
      If (tmpl.isPrivate = 0) Or (tmpl.modId = gm_curModId) Then
        Dim bound(16) As Integer
        Dim boundTy(16) As GmType
        For bi = 0 To tmpl.genCount - 1 Do
          bound(bi) = 0
        End For
        Dim p0 As Integer = g_kids(g_nodes(tmpl.nodeId).extraStart)
        GmUnifyType(tmpl.genStart, tmpl.genCount, g_nodes(p0).tyKind, g_nodes(p0).tyInnerKind, g_nodes(p0).tyNamePos, g_nodes(p0).tyNameLen, g_nodes(p0).tyArgStart, g_nodes(p0).tyArgCount, g_nodes(p0).tyArrLen, t1, e1, bound, boundTy)
        Dim allBound As Boolean = True
        For bi = 0 To tmpl.genCount - 1 Do
          If bound(bi) = 0 Then
            allBound = False
          End If
        End For
        If allBound Then
          outRet = GmInstantiateOp(i, boundTy, tmpl.genCount, gm_curModId)
          Return True
        End If
      End If
    End If
  End For
  Return False
End Function

' =============================================================================
' Expression & Statement Traversal / Rewriting + Type Inference
' =============================================================================

Private Function GmProcessExpr(srcId, doClone As Integer, ByRef outTy As GmType) As Integer
  If srcId < 0 Then
    outTy = GmMakePrim(TyInt)
    Return -1
  End If
  Dim id As Integer = srcId
  If doClone = 1 Then
    id = GmCopyNode(srcId)
  End If
  Dim k As Integer = g_nodes(id).kind

  If k = NdIntLit Then
    outTy = GmMakePrim(TyInt)
    Return id
  End If
  If k = NdFloatLit Then
    outTy = GmMakePrim(TyDouble)
    Return id
  End If
  If k = NdBoolLit Then
    outTy = GmMakePrim(TyBoolean)
    Return id
  End If
  If k = NdStrLit Then
    outTy = GmMakePrim(TyPointer)
    Return id
  End If

  If k = NdStrInterp Then
    Dim cnt As Integer = g_nodes(id).childCount
    If doClone = 1 Then
      Dim sp As Integer = gm_kidSp
      For i = 0 To cnt - 1 Do
        Dim chTy As GmType
        gm_kidStack(gm_kidSp) = GmProcessExpr(g_kids(g_nodes(srcId).firstChild + i), 1, ByRef chTy)
        gm_kidSp = gm_kidSp + 1
      End For
      g_nodes(id).firstChild = g_kidCount
      For i = 0 To cnt - 1 Do
        PushKid(gm_kidStack(sp + i))
      End For
      gm_kidSp = sp
    Else
      For i = 0 To cnt - 1 Do
        Dim chTy As GmType
        g_kids(g_nodes(id).firstChild + i) = GmProcessExpr(g_kids(g_nodes(id).firstChild + i), 0, ByRef chTy)
      End For
    End If
    outTy = GmMakePrim(TyPointer)
    Return id
  End If

  If k = NdWithTarget Then
    If gm_withDepth > gm_withBase Then
      outTy = gm_withTys(gm_withDepth - 1)
    Else
      outTy = GmMakePrim(TyInt)
    End If
    Return id
  End If

  If k = NdVar Then
    Dim nPos = g_nodes(id).intVal, nLen = g_nodes(id).lenVal As Integer
    If g_nodes(id).tyArgCount > 0 Then
      Dim taCnt As Integer = g_nodes(id).tyArgCount
      Dim argTys(16) As GmType
      For i = 0 To taCnt - 1 Do
        Dim tr As Integer = g_kids(g_nodes(id).tyArgStart + i)
        argTys(i) = GmResolveTypeSpec(g_nodes(tr).tyKind, g_nodes(tr).tyInnerKind, 0, g_nodes(tr).tyNamePos, g_nodes(tr).tyNameLen, g_nodes(tr).tyArgStart, g_nodes(tr).tyArgCount)
      End For
      Dim ftIdx As Integer = GmFindFuncTmpl(nPos, nLen)
      If ftIdx < 0 Then
        If GmHasPrivateFuncTmplOther(nPos, nLen) Then
          Printf("Generic Error: Cannot instantiate Private generic function '")
          GmPrintSlice(nPos, nLen)
          Printf("' from included module\n")
        Else
          Printf("Generic Error: Unknown generic function '")
          GmPrintSlice(nPos, nLen)
          Printf("'\n")
        End If
        gm_errCount = gm_errCount + 1
      Else
        If gm_funcTmpls(ftIdx).genCount <> taCnt Then
          Printf("Generic Error: Function '")
          GmPrintSlice(nPos, nLen)
          Printf("' expects %d type argument(s), got %d\n", gm_funcTmpls(ftIdx).genCount, taCnt)
          gm_errCount = gm_errCount + 1
        Else
          Dim mPos = 0, mLen = 0 As Integer
          GmInstantiateFunc(ftIdx, argTys, taCnt, gm_curModId, ByRef mPos, ByRef mLen)
          g_nodes(id).intVal = mPos
          g_nodes(id).lenVal = mLen
        End If
      End If
      g_nodes(id).tyArgStart = 0
      g_nodes(id).tyArgCount = 0
      outTy = GmMakePrim(TyPointer)
      Return id
    End If
    Dim vTy As GmType
    If GmLookupVar(nPos, nLen, ByRef vTy) Then
      outTy = vTy
      Return id
    End If
    If GmFindEnum(nPos, nLen) >= 0 Then
      outTy = GmType With ( kind = TyEnum, innerKind = TyNothing, arrLen = 0, namePos = nPos, nameLen = nLen )
      Return id
    End If
    outTy = GmMakePrim(TyInt)
    Return id
  End If

  If (k = NdNeg) Or (k = NdNot) Then
    Dim inTy As GmType
    g_nodes(id).left = GmProcessExpr(g_nodes(id).left, doClone, ByRef inTy)
    If (inTy.arrLen = 0) And (inTy.kind = TyStruct) Then
      Dim opTok As Integer = TkMinus
      If k = NdNot Then
        opTok = TkNot
      End If
      Dim coi As Integer = GmFindConcreteUnaryOp(opTok, inTy)
      If coi >= 0 Then
        outTy = gm_cOps(coi).retTy
        Return id
      End If
      Dim opRet As GmType
      If GmTryMatchGenericUnaryOp(opTok, inTy, g_nodes(id).left, ByRef opRet) Then
        outTy = opRet
        Return id
      End If
    End If
    outTy = inTy
    Return id
  End If

  If k = NdBinOp Then
    Dim t1 = GmMakePrim(TyInt), t2 = GmMakePrim(TyInt) As GmType
    g_nodes(id).left = GmProcessExpr(g_nodes(id).left, doClone, ByRef t1)
    g_nodes(id).right = GmProcessExpr(g_nodes(id).right, doClone, ByRef t2)
    Dim op As Integer = g_nodes(id).op
    Dim hasStruct As Boolean = ((t1.arrLen = 0) And (t1.kind = TyStruct)) Or ((t2.arrLen = 0) And (t2.kind = TyStruct))
    If hasStruct Then
      Dim coi As Integer = GmFindConcreteBinOp(op, t1, g_nodes(id).left, t2, g_nodes(id).right)
      If coi >= 0 Then
        outTy = gm_cOps(coi).retTy
        Return id
      End If
      Dim opRet As GmType
      If GmTryMatchGenericBinOp(op, t1, g_nodes(id).left, t2, g_nodes(id).right, ByRef opRet) Then
        outTy = opRet
        Return id
      End If
      outTy = t1
      Return id
    End If
    If (op = TkAmp) Or ((op = TkPlus) And (((t1.arrLen = 0) And (t1.kind = TyPointer)) Or ((t1.arrLen <> 0) And (t1.kind = TyByte)))) Then
      outTy = GmMakePrim(TyPointer)
      Return id
    End If
    If (op = TkEq) Or (op = TkNeq) Or (op = TkLt) Or (op = TkGt) Or (op = TkLe) Or (op = TkGe) Then
      outTy = GmMakePrim(TyBoolean)
      Return id
    End If
    If GmIsIntType(t2) And GmIsIntLitNode(g_nodes(id).left) Then
      outTy = t2
      Return id
    End If
    If GmIsFloatType(t2) And GmIsFloatLitNode(g_nodes(id).left) Then
      outTy = t2
      Return id
    End If
    outTy = t1
    Return id
  End If

  If k = NdFieldAccess Then
    Dim baseTy As GmType
    g_nodes(id).left = GmProcessExpr(g_nodes(id).left, doClone, ByRef baseTy)
    If (baseTy.arrLen = 0) And (baseTy.kind = TyStruct) Then
      Dim sIdx As Integer = GmFindConcreteStruct(baseTy.namePos, baseTy.nameLen)
      If sIdx >= 0 Then
        Dim fTy As GmType
        If GmFindStructField(sIdx, g_nodes(id).intVal, g_nodes(id).lenVal, ByRef fTy) >= 0 Then
          outTy = fTy
          Return id
        End If
      End If
    End If
    outTy = GmMakePrim(TyInt)
    Return id
  End If

  If k = NdIndexAccess Then
    Dim baseTy = GmMakePrim(TyInt), idxTy = GmMakePrim(TyInt) As GmType
    g_nodes(id).left = GmProcessExpr(g_nodes(id).left, doClone, ByRef baseTy)
    g_nodes(id).right = GmProcessExpr(g_nodes(id).right, doClone, ByRef idxTy)
    If baseTy.arrLen <> 0 Then
      outTy = baseTy
      outTy.arrLen = 0
    Else
      outTy = GmMakePrim(TyByte)
    End If
    Return id
  End If

  If k = NdArrayLit Then
    Dim cnt As Integer = g_nodes(id).childCount
    Dim firstTy As GmType = GmMakePrim(TyInt)
    If doClone = 1 Then
      Dim sp As Integer = gm_kidSp
      For i = 0 To cnt - 1 Do
        Dim eTy As GmType
        gm_kidStack(gm_kidSp) = GmProcessExpr(g_kids(g_nodes(srcId).firstChild + i), 1, ByRef eTy)
        If i = 0 Then
          firstTy = eTy
        End If
        gm_kidSp = gm_kidSp + 1
      End For
      g_nodes(id).firstChild = g_kidCount
      For i = 0 To cnt - 1 Do
        PushKid(gm_kidStack(sp + i))
      End For
      gm_kidSp = sp
    Else
      For i = 0 To cnt - 1 Do
        Dim eTy As GmType
        g_kids(g_nodes(id).firstChild + i) = GmProcessExpr(g_kids(g_nodes(id).firstChild + i), 0, ByRef eTy)
        If i = 0 Then
          firstTy = eTy
        End If
      End For
    End If
    outTy = firstTy
    outTy.arrLen = cnt
    Return id
  End If

  If k = NdStructInit Then
    Dim cnt As Integer = g_nodes(id).childCount
    Dim fiTys(64) As GmType
    If doClone = 1 Then
      Dim sp As Integer = gm_kidSp
      For i = 0 To cnt - 1 Do
        Dim srcFi As Integer = g_kids(g_nodes(srcId).firstChild + i)
        Dim newFi As Integer = GmCopyNode(srcFi)
        g_nodes(newFi).left = GmProcessExpr(g_nodes(srcFi).left, 1, ByRef fiTys(i))
        gm_kidStack(gm_kidSp) = newFi
        gm_kidSp = gm_kidSp + 1
      End For
      g_nodes(id).firstChild = g_kidCount
      For i = 0 To cnt - 1 Do
        PushKid(gm_kidStack(sp + i))
      End For
      gm_kidSp = sp
    Else
      For i = 0 To cnt - 1 Do
        Dim fi As Integer = g_kids(g_nodes(id).firstChild + i)
        g_nodes(fi).left = GmProcessExpr(g_nodes(fi).left, 0, ByRef fiTys(i))
      End For
    End If

    Dim sPos = g_nodes(id).intVal, sLen = g_nodes(id).lenVal As Integer
    If g_nodes(id).tyArgCount > 0 Then
      Dim res As GmType = GmResolveTypeSpec(TyStruct, TyNothing, 0, sPos, sLen, g_nodes(id).tyArgStart, g_nodes(id).tyArgCount)
      g_nodes(id).intVal = res.namePos
      g_nodes(id).lenVal = res.nameLen
      g_nodes(id).tyArgStart = 0
      g_nodes(id).tyArgCount = 0
      outTy = res
      Return id
    End If
    Dim subTy As GmType
    If GmLookupSubst(sPos, sLen, ByRef subTy) Then
      g_nodes(id).intVal = subTy.namePos
      g_nodes(id).lenVal = subTy.nameLen
      outTy = subTy
      Return id
    End If
    ' Try inferring generic struct from named field initializers (e.g., Box With ( value = 42 ))
    Dim stIdx As Integer = GmFindStructTmpl(sPos, sLen)
    If (stIdx >= 0) And (GmFindConcreteStruct(sPos, sLen) < 0) Then
      Dim tmpl As GmStructTmpl = gm_structTmpls(stIdx)
      Dim bound(16) As Integer
      Dim boundTy(16) As GmType
      For bi = 0 To tmpl.genCount - 1 Do
        bound(bi) = 0
      End For
      Dim tFc As Integer = g_nodes(tmpl.nodeId).childCount
      For i = 0 To cnt - 1 Do
        Dim fi As Integer = g_kids(g_nodes(id).firstChild + i)
        For fj = 0 To tFc - 1 Do
          Dim fd As Integer = g_kids(g_nodes(tmpl.nodeId).firstChild + fj)
          If SlicesEqual(g_nodes(fd).intVal, g_nodes(fd).lenVal, g_nodes(fi).intVal, g_nodes(fi).lenVal) Then
            GmUnifyType(tmpl.genStart, tmpl.genCount, g_nodes(fd).tyKind, g_nodes(fd).tyInnerKind, g_nodes(fd).tyNamePos, g_nodes(fd).tyNameLen, g_nodes(fd).tyArgStart, g_nodes(fd).tyArgCount, g_nodes(fd).tyArrLen, fiTys(i), g_nodes(fi).left, bound, boundTy)
          End If
        End For
      End For
      Dim allBound As Boolean = True
      For bi = 0 To tmpl.genCount - 1 Do
        If bound(bi) = 0 Then
          allBound = False
        End If
      End For
      If allBound Then
        Dim mPos = 0, mLen = 0 As Integer
        GmInstantiateStruct(stIdx, boundTy, tmpl.genCount, gm_curModId, ByRef mPos, ByRef mLen)
        g_nodes(id).intVal = mPos
        g_nodes(id).lenVal = mLen
        outTy = GmType With ( kind = TyStruct, innerKind = TyNothing, arrLen = 0, namePos = mPos, nameLen = mLen )
        Return id
      End If
      Printf("Generic Error: Cannot infer type arguments for generic structure '")
      GmPrintSlice(sPos, sLen)
      Printf("'; specify explicit (Of ...)\n")
      gm_errCount = gm_errCount + 1
    End If
    outTy = GmType With ( kind = TyStruct, innerKind = TyNothing, arrLen = 0, namePos = sPos, nameLen = sLen )
    Return id
  End If

  If k = NdSizeOf Then
    GmApplyResolvedType(id)
    outTy = GmMakePrim(TyInt)
    Return id
  End If

  If k = NdCast Then
    Dim inTy As GmType
    g_nodes(id).left = GmProcessExpr(g_nodes(id).left, doClone, ByRef inTy)
    outTy = GmMakePrim(g_nodes(id).tyKind)
    Return id
  End If

  If k = NdArenaAlloc Then
    Dim aTy = GmMakePrim(TyArena), cTy = GmMakePrim(TyInt) As GmType
    g_nodes(id).left = GmProcessExpr(g_nodes(id).left, doClone, ByRef aTy)
    g_nodes(id).right = GmProcessExpr(g_nodes(id).right, doClone, ByRef cTy)
    Dim eTy As GmType = GmApplyResolvedType(id)
    eTy.arrLen = -1
    outTy = eTy
    Return id
  End If

  If k = NdHeapAlloc Then
    Dim cTy As GmType
    g_nodes(id).left = GmProcessExpr(g_nodes(id).left, doClone, ByRef cTy)
    Dim eTy As GmType = GmApplyResolvedType(id)
    eTy.arrLen = -1
    outTy = eTy
    Return id
  End If

  If k = NdHeapRealloc Then
    Dim pTy = GmMakePrim(TyPointer), cTy = GmMakePrim(TyInt) As GmType
    g_nodes(id).left = GmProcessExpr(g_nodes(id).left, doClone, ByRef pTy)
    g_nodes(id).right = GmProcessExpr(g_nodes(id).right, doClone, ByRef cTy)
    Dim eTy As GmType = GmApplyResolvedType(id)
    eTy.arrLen = -1
    outTy = eTy
    Return id
  End If

  If (k = NdVolatileLoad) Or (k = NdPtrRead) Then
    Dim aTy As GmType
    g_nodes(id).left = GmProcessExpr(g_nodes(id).left, doClone, ByRef aTy)
    outTy = GmApplyResolvedType(id)
    Return id
  End If

  If (k = NdVolatileStore) Or (k = NdPtrWrite) Then
    Dim aTy = GmMakePrim(TyPointer), vTy = GmMakePrim(TyInt) As GmType
    g_nodes(id).left = GmProcessExpr(g_nodes(id).left, doClone, ByRef aTy)
    g_nodes(id).right = GmProcessExpr(g_nodes(id).right, doClone, ByRef vTy)
    GmApplyResolvedType(id)
    outTy = GmMakePrim(TyNothing)
    Return id
  End If

  If k = NdPtrAdd Then
    Dim pTy = GmMakePrim(TyPointer), oTy = GmMakePrim(TyInt) As GmType
    g_nodes(id).left = GmProcessExpr(g_nodes(id).left, doClone, ByRef pTy)
    g_nodes(id).right = GmProcessExpr(g_nodes(id).right, doClone, ByRef oTy)
    outTy = GmMakePrim(TyPointer)
    Return id
  End If

  If k = NdAddressOf Then
    Dim tTy As GmType
    g_nodes(id).left = GmProcessExpr(g_nodes(id).left, doClone, ByRef tTy)
    outTy = GmMakePrim(TyPointer)
    Return id
  End If

  If k = NdAwait Then
    Dim tTy As GmType
    g_nodes(id).left = GmProcessExpr(g_nodes(id).left, doClone, ByRef tTy)
    If tTy.kind = TyTask Then
      outTy = GmType With ( kind = tTy.innerKind, innerKind = TyNothing, arrLen = 0, namePos = tTy.namePos, nameLen = tTy.nameLen )
    Else
      outTy = GmMakePrim(TyInt)
    End If
    Return id
  End If

  If k = NdCall Then
    Dim aCnt As Integer = g_nodes(id).childCount
    Dim aTys(64) As GmType
    If doClone = 1 Then
      Dim sp As Integer = gm_kidSp
      For i = 0 To aCnt - 1 Do
        Dim srcA As Integer = g_kids(g_nodes(srcId).firstChild + i)
        Dim newA As Integer = GmCopyNode(srcA)
        g_nodes(newA).left = GmProcessExpr(g_nodes(srcA).left, 1, ByRef aTys(i))
        gm_kidStack(gm_kidSp) = newA
        gm_kidSp = gm_kidSp + 1
      End For
      g_nodes(id).firstChild = g_kidCount
      For i = 0 To aCnt - 1 Do
        PushKid(gm_kidStack(sp + i))
      End For
      gm_kidSp = sp
    Else
      For i = 0 To aCnt - 1 Do
        Dim aNode As Integer = g_kids(g_nodes(id).firstChild + i)
        g_nodes(aNode).left = GmProcessExpr(g_nodes(aNode).left, 0, ByRef aTys(i))
      End For
    End If

    Dim nPos = g_nodes(id).intVal, nLen = g_nodes(id).lenVal As Integer

    ' Case 1: Explicit generic arguments Name(Of T1, T2)(args...)
    If g_nodes(id).tyArgCount > 0 Then
      Dim taCnt As Integer = g_nodes(id).tyArgCount
      Dim argTys(16) As GmType
      For i = 0 To taCnt - 1 Do
        Dim tr As Integer = g_kids(g_nodes(id).tyArgStart + i)
        argTys(i) = GmResolveTypeSpec(g_nodes(tr).tyKind, g_nodes(tr).tyInnerKind, 0, g_nodes(tr).tyNamePos, g_nodes(tr).tyNameLen, g_nodes(tr).tyArgStart, g_nodes(tr).tyArgCount)
      End For
      g_nodes(id).tyArgStart = 0
      g_nodes(id).tyArgCount = 0

      Dim stIdx As Integer = GmFindStructTmpl(nPos, nLen)
      If stIdx >= 0 Then
        If gm_structTmpls(stIdx).genCount <> taCnt Then
          Printf("Generic Error: Structure '")
          GmPrintSlice(nPos, nLen)
          Printf("' expects %d type argument(s), got %d\n", gm_structTmpls(stIdx).genCount, taCnt)
          gm_errCount = gm_errCount + 1
          outTy = GmMakePrim(TyInt)
          Return id
        End If
        Dim mPos = 0, mLen = 0 As Integer
        GmInstantiateStruct(stIdx, argTys, taCnt, gm_curModId, ByRef mPos, ByRef mLen)
        g_nodes(id).intVal = mPos
        g_nodes(id).lenVal = mLen
        outTy = GmType With ( kind = TyStruct, innerKind = TyNothing, arrLen = 0, namePos = mPos, nameLen = mLen )
        Return id
      End If

      Dim ftIdx As Integer = GmFindFuncTmpl(nPos, nLen)
      If ftIdx >= 0 Then
        If gm_funcTmpls(ftIdx).genCount <> taCnt Then
          Printf("Generic Error: Function '")
          GmPrintSlice(nPos, nLen)
          Printf("' expects %d type argument(s), got %d\n", gm_funcTmpls(ftIdx).genCount, taCnt)
          gm_errCount = gm_errCount + 1
          outTy = GmMakePrim(TyInt)
          Return id
        End If
        Dim mPos = 0, mLen = 0 As Integer
        GmInstantiateFunc(ftIdx, argTys, taCnt, gm_curModId, ByRef mPos, ByRef mLen)
        g_nodes(id).intVal = mPos
        g_nodes(id).lenVal = mLen
        Dim cfIdx As Integer = GmFindConcreteFunc(mPos, mLen)
        If cfIdx >= 0 Then
          outTy = gm_cFuncs(cfIdx).retTy
        Else
          outTy = GmMakePrim(TyInt)
        End If
        Return id
      End If

      If GmHasPrivateStructTmplOther(nPos, nLen) Or GmHasPrivateFuncTmplOther(nPos, nLen) Then
        Printf("Generic Error: Cannot instantiate Private generic symbol '")
        GmPrintSlice(nPos, nLen)
        Printf("' from included module\n")
      Else
        Printf("Generic Error: Unknown generic function or structure '")
        GmPrintSlice(nPos, nLen)
        Printf("'\n")
      End If
      gm_errCount = gm_errCount + 1
      outTy = GmMakePrim(TyInt)
      Return id
    End If

    ' Case 2: Type parameter used as cast or struct constructor: T(x)
    Dim subTy As GmType
    If GmLookupSubst(nPos, nLen, ByRef subTy) Then
      If (subTy.kind = TyStruct) And (subTy.arrLen = 0) Then
        g_nodes(id).intVal = subTy.namePos
        g_nodes(id).lenVal = subTy.nameLen
        outTy = subTy
        Return id
      End If
      If (aCnt = 1) And (subTy.arrLen = 0) Then
        Dim a0 As Integer = g_kids(g_nodes(id).firstChild)
        If (g_nodes(a0).op = 0) And ((subTy.kind = TyByte) Or (subTy.kind = TyShort) Or (subTy.kind = TyInt) Or (subTy.kind = TyLong) Or (subTy.kind = TySingle) Or (subTy.kind = TyDouble) Or (subTy.kind = TyBoolean) Or (subTy.kind = TyPointer)) Then
          Dim castId As Integer = NewNode(NdCast)
          g_nodes(castId).tyKind = subTy.kind
          g_nodes(castId).left = g_nodes(a0).left
          outTy = subTy
          Return castId
        End If
      End If
    End If

    ' Case 3: Builtin calls
    If GmIsBuiltinName(nPos, nLen) Then
      If SliceEqualsStr(nPos, nLen, "IsNull", 6) Then
        outTy = GmMakePrim(TyBoolean)
      Else
        If SliceEqualsStr(nPos, nLen, "ArenaCreate", 11) Or SliceEqualsStr(nPos, nLen, "ArenaFromBuffer", 15) Then
          outTy = GmMakePrim(TyArena)
        Else
          If SliceEqualsStr(nPos, nLen, "ArenaMark", 9) Or SliceEqualsStr(nPos, nLen, "ArenaUsed", 9) Or SliceEqualsStr(nPos, nLen, "ArenaRemaining", 14) Or SliceEqualsStr(nPos, nLen, "ArenaCapacity", 13) Then
            outTy = GmMakePrim(TyLong)
          Else
            If SliceEqualsStr(nPos, nLen, "ArenaAllocBytes", 15) Or SliceEqualsStr(nPos, nLen, "HeapAllocBytes", 14) Or SliceEqualsStr(nPos, nLen, "Malloc", 6) Or SliceEqualsStr(nPos, nLen, "Calloc", 6) Or SliceEqualsStr(nPos, nLen, "Realloc", 7) Or SliceEqualsStr(nPos, nLen, "Fopen", 5) Then
              outTy = GmMakePrim(TyPointer)
            Else
              outTy = GmMakePrim(TyInt)
            End If
          End If
        End If
      End If
      Return id
    End If

    ' Case 4: Array / Pointer indexing parsed as Call: var(idx)
    If aCnt = 1 Then
      Dim a0 As Integer = g_kids(g_nodes(id).firstChild)
      Dim vTy As GmType
      If (g_nodes(a0).op = 0) And GmLookupVar(nPos, nLen, ByRef vTy) Then
        If vTy.arrLen <> 0 Then
          outTy = vTy
          outTy.arrLen = 0
          Return id
        End If
        If vTy.kind = TyPointer Then
          outTy = GmMakePrim(TyByte)
          Return id
        End If
      End If
    End If

    ' Case 5: Concrete struct constructor
    Dim csIdx As Integer = GmFindConcreteStruct(nPos, nLen)
    If csIdx >= 0 Then
      outTy = GmType With ( kind = TyStruct, innerKind = TyNothing, arrLen = 0, namePos = nPos, nameLen = nLen )
      Return id
    End If

    ' Case 6: Concrete function call
    Dim cfIdx2 As Integer = GmFindConcreteFunc(nPos, nLen)
    If cfIdx2 >= 0 Then
      outTy = gm_cFuncs(cfIdx2).retTy
      Return id
    End If

    ' Case 7: Call-site type inference for generic function
    Dim ftIdx2 As Integer = GmFindFuncTmpl(nPos, nLen)
    If ftIdx2 >= 0 Then
      Dim tmpl As GmFuncTmpl = gm_funcTmpls(ftIdx2)
      Dim bound(16) As Integer
      Dim boundTy(16) As GmType
      For bi = 0 To tmpl.genCount - 1 Do
        bound(bi) = 0
      End For
      Dim pCnt As Integer = g_nodes(tmpl.nodeId).extraCount
      Dim lim As Integer = aCnt
      If pCnt < lim Then
        lim = pCnt
      End If
      For i = 0 To lim - 1 Do
        Dim pNode As Integer = g_kids(g_nodes(tmpl.nodeId).extraStart + i)
        Dim aNode As Integer = g_kids(g_nodes(id).firstChild + i)
        GmUnifyType(tmpl.genStart, tmpl.genCount, g_nodes(pNode).tyKind, g_nodes(pNode).tyInnerKind, g_nodes(pNode).tyNamePos, g_nodes(pNode).tyNameLen, g_nodes(pNode).tyArgStart, g_nodes(pNode).tyArgCount, g_nodes(pNode).tyArrLen, aTys(i), g_nodes(aNode).left, bound, boundTy)
      End For
      Dim missingIdx As Integer = -1
      For bi = 0 To tmpl.genCount - 1 Do
        If (bound(bi) = 0) And (missingIdx < 0) Then
          missingIdx = bi
        End If
      End For
      If missingIdx >= 0 Then
        Dim mp As Integer = g_kids(tmpl.genStart + missingIdx)
        Printf("Generic Error: Cannot infer type parameter '")
        GmPrintSlice(g_nodes(mp).tyNamePos, g_nodes(mp).tyNameLen)
        Printf("' for '")
        GmPrintSlice(nPos, nLen)
        Printf("'; specify explicit type arguments with (Of ...)\n")
        gm_errCount = gm_errCount + 1
        outTy = GmMakePrim(TyInt)
        Return id
      End If
      Dim mPos = 0, mLen = 0 As Integer
      GmInstantiateFunc(ftIdx2, boundTy, tmpl.genCount, gm_curModId, ByRef mPos, ByRef mLen)
      g_nodes(id).intVal = mPos
      g_nodes(id).lenVal = mLen
      Dim cfNew As Integer = GmFindConcreteFunc(mPos, mLen)
      If cfNew >= 0 Then
        outTy = gm_cFuncs(cfNew).retTy
      Else
        outTy = GmMakePrim(TyInt)
      End If
      Return id
    End If

    ' Case 8: Call-site type inference for generic struct positional constructor
    Dim stIdx2 As Integer = GmFindStructTmpl(nPos, nLen)
    If stIdx2 >= 0 Then
      Dim tmpl As GmStructTmpl = gm_structTmpls(stIdx2)
      Dim bound(16) As Integer
      Dim boundTy(16) As GmType
      For bi = 0 To tmpl.genCount - 1 Do
        bound(bi) = 0
      End For
      Dim fCnt As Integer = g_nodes(tmpl.nodeId).childCount
      Dim lim As Integer = aCnt
      If fCnt < lim Then
        lim = fCnt
      End If
      For i = 0 To lim - 1 Do
        Dim fNode As Integer = g_kids(g_nodes(tmpl.nodeId).firstChild + i)
        Dim aNode As Integer = g_kids(g_nodes(id).firstChild + i)
        GmUnifyType(tmpl.genStart, tmpl.genCount, g_nodes(fNode).tyKind, g_nodes(fNode).tyInnerKind, g_nodes(fNode).tyNamePos, g_nodes(fNode).tyNameLen, g_nodes(fNode).tyArgStart, g_nodes(fNode).tyArgCount, g_nodes(fNode).tyArrLen, aTys(i), g_nodes(aNode).left, bound, boundTy)
      End For
      Dim allBound As Boolean = True
      For bi = 0 To tmpl.genCount - 1 Do
        If bound(bi) = 0 Then
          allBound = False
        End If
      End For
      If allBound Then
        Dim mPos = 0, mLen = 0 As Integer
        GmInstantiateStruct(stIdx2, boundTy, tmpl.genCount, gm_curModId, ByRef mPos, ByRef mLen)
        g_nodes(id).intVal = mPos
        g_nodes(id).lenVal = mLen
        outTy = GmType With ( kind = TyStruct, innerKind = TyNothing, arrLen = 0, namePos = mPos, nameLen = mLen )
        Return id
      End If
      Printf("Generic Error: Cannot infer type arguments for generic structure '")
      GmPrintSlice(nPos, nLen)
      Printf("'; specify explicit (Of ...)\n")
      gm_errCount = gm_errCount + 1
      outTy = GmMakePrim(TyInt)
      Return id
    End If

    outTy = GmMakePrim(TyInt)
    Return id
  End If

  outTy = GmMakePrim(TyInt)
  Return id
End Function

Private Function GmProcessStmt(srcId, doClone As Integer) As Integer
  If srcId < 0 Then
    Return -1
  End If
  Dim id As Integer = srcId
  If doClone = 1 Then
    id = GmCopyNode(srcId)
  End If
  Dim k As Integer = g_nodes(id).kind

  If (k = NdLabel) Or (k = NdGoTo) Or (k = NdBreak) Or (k = NdContinue) Then
    Return id
  End If

  If (k = NdDim) Or (k = NdStaticDim) Then
    Dim declTy As GmType = GmApplyResolvedType(id)
    If g_nodes(id).left >= 0 Then
      Dim initTy As GmType
      g_nodes(id).left = GmProcessExpr(g_nodes(id).left, doClone, ByRef initTy)
    End If
    If g_nodes(id).right >= 0 Then
      Dim atTy As GmType
      g_nodes(id).right = GmProcessExpr(g_nodes(id).right, doClone, ByRef atTy)
    End If
    gm_locals(gm_localCount) = GmLocalSym With ( namePos = g_nodes(id).intVal, nameLen = g_nodes(id).lenVal, ty = declTy )
    gm_localCount = gm_localCount + 1
    Return id
  End If

  If (k = NdAssign) Or (k = NdExprStmt) Or (k = NdReturn) Then
    If g_nodes(id).left >= 0 Then
      Dim eTy As GmType
      g_nodes(id).left = GmProcessExpr(g_nodes(id).left, doClone, ByRef eTy)
    End If
    Return id
  End If

  If (k = NdFieldAssign) Or (k = NdAssert) Then
    Dim t1 = GmMakePrim(TyInt), t2 = GmMakePrim(TyInt) As GmType
    g_nodes(id).left = GmProcessExpr(g_nodes(id).left, doClone, ByRef t1)
    If g_nodes(id).right >= 0 Then
      g_nodes(id).right = GmProcessExpr(g_nodes(id).right, doClone, ByRef t2)
    End If
    Return id
  End If

  If k = NdIndexAssign Then
    Dim t1 = GmMakePrim(TyInt), t2 = GmMakePrim(TyInt), t3 = GmMakePrim(TyInt) As GmType
    g_nodes(id).left = GmProcessExpr(g_nodes(id).left, doClone, ByRef t1)
    g_nodes(id).right = GmProcessExpr(g_nodes(id).right, doClone, ByRef t2)
    g_nodes(id).third = GmProcessExpr(g_nodes(id).third, doClone, ByRef t3)
    Return id
  End If

  If k = NdIf Then
    Dim cTy As GmType
    g_nodes(id).left = GmProcessExpr(g_nodes(id).left, doClone, ByRef cTy)
    Dim savedLocals As Integer = gm_localCount
    Dim tCnt As Integer = g_nodes(id).childCount
    If doClone = 1 Then
      Dim sp As Integer = gm_kidSp
      For i = 0 To tCnt - 1 Do
        gm_kidStack(gm_kidSp) = GmProcessStmt(g_kids(g_nodes(srcId).firstChild + i), 1)
        gm_kidSp = gm_kidSp + 1
      End For
      g_nodes(id).firstChild = g_kidCount
      For i = 0 To tCnt - 1 Do
        PushKid(gm_kidStack(sp + i))
      End For
      gm_kidSp = sp
    Else
      For i = 0 To tCnt - 1 Do
        g_kids(g_nodes(id).firstChild + i) = GmProcessStmt(g_kids(g_nodes(id).firstChild + i), 0)
      End For
    End If
    gm_localCount = savedLocals

    Dim eCnt As Integer = g_nodes(id).extraCount
    If doClone = 1 Then
      Dim sp As Integer = gm_kidSp
      For i = 0 To eCnt - 1 Do
        gm_kidStack(gm_kidSp) = GmProcessStmt(g_kids(g_nodes(srcId).extraStart + i), 1)
        gm_kidSp = gm_kidSp + 1
      End For
      g_nodes(id).extraStart = g_kidCount
      For i = 0 To eCnt - 1 Do
        PushKid(gm_kidStack(sp + i))
      End For
      gm_kidSp = sp
    Else
      For i = 0 To eCnt - 1 Do
        g_kids(g_nodes(id).extraStart + i) = GmProcessStmt(g_kids(g_nodes(id).extraStart + i), 0)
      End For
    End If
    gm_localCount = savedLocals
    Return id
  End If

  If k = NdWhile Then
    Dim cTy As GmType
    g_nodes(id).left = GmProcessExpr(g_nodes(id).left, doClone, ByRef cTy)
    Dim savedLocals As Integer = gm_localCount
    Dim bCnt As Integer = g_nodes(id).childCount
    If doClone = 1 Then
      Dim sp As Integer = gm_kidSp
      For i = 0 To bCnt - 1 Do
        gm_kidStack(gm_kidSp) = GmProcessStmt(g_kids(g_nodes(srcId).firstChild + i), 1)
        gm_kidSp = gm_kidSp + 1
      End For
      g_nodes(id).firstChild = g_kidCount
      For i = 0 To bCnt - 1 Do
        PushKid(gm_kidStack(sp + i))
      End For
      gm_kidSp = sp
    Else
      For i = 0 To bCnt - 1 Do
        g_kids(g_nodes(id).firstChild + i) = GmProcessStmt(g_kids(g_nodes(id).firstChild + i), 0)
      End For
    End If
    gm_localCount = savedLocals
    Return id
  End If

  If k = NdFor Then
    Dim sTy = GmMakePrim(TyInt), eTy = GmMakePrim(TyInt) As GmType
    g_nodes(id).left = GmProcessExpr(g_nodes(id).left, doClone, ByRef sTy)
    g_nodes(id).right = GmProcessExpr(g_nodes(id).right, doClone, ByRef eTy)
    Dim savedLocals As Integer = gm_localCount
    gm_locals(gm_localCount) = GmLocalSym With ( namePos = g_nodes(id).intVal, nameLen = g_nodes(id).lenVal, ty = GmMakePrim(TyInt) )
    gm_localCount = gm_localCount + 1
    Dim bCnt As Integer = g_nodes(id).childCount
    If doClone = 1 Then
      Dim sp As Integer = gm_kidSp
      For i = 0 To bCnt - 1 Do
        gm_kidStack(gm_kidSp) = GmProcessStmt(g_kids(g_nodes(srcId).firstChild + i), 1)
        gm_kidSp = gm_kidSp + 1
      End For
      g_nodes(id).firstChild = g_kidCount
      For i = 0 To bCnt - 1 Do
        PushKid(gm_kidStack(sp + i))
      End For
      gm_kidSp = sp
    Else
      For i = 0 To bCnt - 1 Do
        g_kids(g_nodes(id).firstChild + i) = GmProcessStmt(g_kids(g_nodes(id).firstChild + i), 0)
      End For
    End If
    gm_localCount = savedLocals
    Return id
  End If

  If k = NdWith Then
    Dim tgtTy As GmType
    g_nodes(id).left = GmProcessExpr(g_nodes(id).left, doClone, ByRef tgtTy)
    If gm_withDepth < 64 Then
      gm_withTys(gm_withDepth) = tgtTy
      gm_withDepth = gm_withDepth + 1
      Dim savedLocals As Integer = gm_localCount
      Dim bCnt As Integer = g_nodes(id).childCount
      If doClone = 1 Then
        Dim sp As Integer = gm_kidSp
        For i = 0 To bCnt - 1 Do
          gm_kidStack(gm_kidSp) = GmProcessStmt(g_kids(g_nodes(srcId).firstChild + i), 1)
          gm_kidSp = gm_kidSp + 1
        End For
        g_nodes(id).firstChild = g_kidCount
        For i = 0 To bCnt - 1 Do
          PushKid(gm_kidStack(sp + i))
        End For
        gm_kidSp = sp
      Else
        For i = 0 To bCnt - 1 Do
          g_kids(g_nodes(id).firstChild + i) = GmProcessStmt(g_kids(g_nodes(id).firstChild + i), 0)
        End For
      End If
      gm_localCount = savedLocals
      gm_withDepth = gm_withDepth - 1
    End If
    Return id
  End If

  If k = NdMatch Then
    Dim sTy As GmType
    g_nodes(id).left = GmProcessExpr(g_nodes(id).left, doClone, ByRef sTy)
    Dim armCnt As Integer = g_nodes(id).childCount
    Dim spArms As Integer = gm_kidSp
    For ai = 0 To armCnt - 1 Do
      Dim srcArm As Integer = g_kids(g_nodes(srcId).firstChild + ai)
      Dim armId As Integer = srcArm
      If doClone = 1 Then
        armId = GmCopyNode(srcArm)
      End If
      ' Pattern values
      Dim vCnt As Integer = g_nodes(armId).extraCount
      If doClone = 1 Then
        Dim spV As Integer = gm_kidSp
        For vi = 0 To vCnt - 1 Do
          Dim pTy As GmType
          gm_kidStack(gm_kidSp) = GmProcessExpr(g_kids(g_nodes(srcArm).extraStart + vi), 1, ByRef pTy)
          gm_kidSp = gm_kidSp + 1
        End For
        g_nodes(armId).extraStart = g_kidCount
        For vi = 0 To vCnt - 1 Do
          PushKid(gm_kidStack(spV + vi))
        End For
        gm_kidSp = spV
      Else
        For vi = 0 To vCnt - 1 Do
          Dim pTy As GmType
          g_kids(g_nodes(armId).extraStart + vi) = GmProcessExpr(g_kids(g_nodes(armId).extraStart + vi), 0, ByRef pTy)
        End For
      End If
      ' Guard
      If g_nodes(armId).left >= 0 Then
        Dim gTy As GmType
        g_nodes(armId).left = GmProcessExpr(g_nodes(armId).left, doClone, ByRef gTy)
      End If
      ' Body
      Dim savedLocals As Integer = gm_localCount
      Dim bCnt As Integer = g_nodes(armId).childCount
      If doClone = 1 Then
        Dim spB As Integer = gm_kidSp
        For bi = 0 To bCnt - 1 Do
          gm_kidStack(gm_kidSp) = GmProcessStmt(g_kids(g_nodes(srcArm).firstChild + bi), 1)
          gm_kidSp = gm_kidSp + 1
        End For
        g_nodes(armId).firstChild = g_kidCount
        For bi = 0 To bCnt - 1 Do
          PushKid(gm_kidStack(spB + bi))
        End For
        gm_kidSp = spB
        gm_kidStack(gm_kidSp) = armId
        gm_kidSp = gm_kidSp + 1
      Else
        For bi = 0 To bCnt - 1 Do
          g_kids(g_nodes(armId).firstChild + bi) = GmProcessStmt(g_kids(g_nodes(armId).firstChild + bi), 0)
        End For
      End If
      gm_localCount = savedLocals
    End For
    If doClone = 1 Then
      g_nodes(id).firstChild = g_kidCount
      For ai = 0 To armCnt - 1 Do
        PushKid(gm_kidStack(spArms + ai))
      End For
      gm_kidSp = spArms
    End If
    Return id
  End If

  Return id
End Function

' =============================================================================
' Main Entry Point
' =============================================================================

Private Sub GmValidateGenParams(gStart, gCount, ownerPos, ownerLen As Integer)
  For i = 0 To gCount - 1 Do
    Dim tp1 As Integer = g_kids(gStart + i)
    For j = 0 To i - 1 Do
      Dim tp2 As Integer = g_kids(gStart + j)
      If SlicesEqual(g_nodes(tp1).tyNamePos, g_nodes(tp1).tyNameLen, g_nodes(tp2).tyNamePos, g_nodes(tp2).tyNameLen) Then
        Printf("Generic Error: Duplicate type parameter '")
        GmPrintSlice(g_nodes(tp1).tyNamePos, g_nodes(tp1).tyNameLen)
        If ownerLen > 0 Then
          Printf("' in '")
          GmPrintSlice(ownerPos, ownerLen)
          Printf("'\n")
        Else
          Printf("'\n")
        End If
        gm_errCount = gm_errCount + 1
      End If
    End For
  End For
End Sub

Public Function MonomorphizeProgram(ByRef arena As Arena, progRoot As Integer) As Integer
  Dim defStart As Integer = g_nodes(progRoot).firstChild
  Dim defCount As Integer = g_nodes(progRoot).childCount

  ' Fast-path check: if no generic templates and no type arguments exist anywhere, return immediately
  Dim hasGenerics As Integer = 0
  For i = 0 To defCount - 1 Do
    Dim d As Integer = g_kids(defStart + i)
    If g_nodes(d).genCount > 0 Then
      hasGenerics = 1
    End If
  End For
  If hasGenerics = 0 Then
    For ni = 0 To g_nodeCount - 1 Do
      If g_nodes(ni).tyArgCount > 0 Then
        hasGenerics = 1
        Break
      End If
    End For
  End If
  If hasGenerics = 0 Then
    Return 0
  End If

  gm_structTmpls = ArenaAlloc(ByRef arena, GmStructTmpl, 256)
  gm_funcTmpls = ArenaAlloc(ByRef arena, GmFuncTmpl, 512)
  gm_opTmpls = ArenaAlloc(ByRef arena, GmOpTmpl, 128)
  gm_structSpecs = ArenaAlloc(ByRef arena, GmStructSpec, 512)
  gm_funcSpecs = ArenaAlloc(ByRef arena, GmFuncSpec, 1024)
  gm_opSpecs = ArenaAlloc(ByRef arena, GmOpSpec, 256)
  gm_specArgs = ArenaAlloc(ByRef arena, GmType, 4096)
  gm_enums = ArenaAlloc(ByRef arena, GmEnumInfo, 128)
  gm_cStructs = ArenaAlloc(ByRef arena, GmConcreteStruct, 512)
  gm_cFields = ArenaAlloc(ByRef arena, GmFieldInfo, 4096)
  gm_globals = ArenaAlloc(ByRef arena, GmGlobalInfo, 1024)
  gm_cFuncs = ArenaAlloc(ByRef arena, GmFuncInfo, 2048)
  gm_cOps = ArenaAlloc(ByRef arena, GmOpInfo, 512)
  gm_locals = ArenaAlloc(ByRef arena, GmLocalSym, 512)
  gm_subst = ArenaAlloc(ByRef arena, GmSubstEntry, 128)
  gm_kidStack = ArenaAlloc(ByRef arena, Integer, 65536)
  gm_newDefs = ArenaAlloc(ByRef arena, Integer, 4096)

  gm_structTmplCount = 0
  gm_funcTmplCount = 0
  gm_opTmplCount = 0
  gm_structSpecCount = 0
  gm_funcSpecCount = 0
  gm_opSpecCount = 0
  gm_specArgCount = 0
  gm_enumCount = 0
  gm_cStructCount = 0
  gm_cFieldCount = 0
  gm_globalCount = 0
  gm_cFuncCount = 0
  gm_cOpCount = 0
  gm_localBase = 0
  gm_localCount = 0
  gm_substBase = 0
  gm_substCount = 0
  gm_withBase = 0
  gm_withDepth = 0
  gm_kidSp = 0
  gm_newDefCount = 0
  gm_depth = 0
  gm_curModId = 0
  gm_altModId = -1
  gm_errCount = 0

  ' Pass 1: Register templates (genCount > 0) and collect concrete defs
  For i = 0 To defCount - 1 Do
    Dim d As Integer = g_kids(defStart + i)
    Dim dk As Integer = g_nodes(d).kind
    Dim isPriv As Integer = 0
    If g_nodes(d).op = 2 Then
      isPriv = 1
    End If

    If dk = NdEnumDef Then
      gm_enums(gm_enumCount) = GmEnumInfo With ( namePos = g_nodes(d).intVal, nameLen = g_nodes(d).lenVal, isPrivate = isPriv, modId = g_nodes(d).modId )
      gm_enumCount = gm_enumCount + 1
      gm_newDefs(gm_newDefCount) = d
      gm_newDefCount = gm_newDefCount + 1
    End If

    If dk = NdStructDef Then
      If g_nodes(d).genCount > 0 Then
        GmValidateGenParams(g_nodes(d).genStart, g_nodes(d).genCount, g_nodes(d).intVal, g_nodes(d).lenVal)
        gm_structTmpls(gm_structTmplCount) = GmStructTmpl With (
          nodeId = d,
          namePos = g_nodes(d).intVal,
          nameLen = g_nodes(d).lenVal,
          genStart = g_nodes(d).genStart,
          genCount = g_nodes(d).genCount,
          isPrivate = isPriv,
          modId = g_nodes(d).modId
        )
        gm_structTmplCount = gm_structTmplCount + 1
      Else
        gm_cStructs(gm_cStructCount) = GmConcreteStruct With (
          namePos = g_nodes(d).intVal,
          nameLen = g_nodes(d).lenVal,
          fieldStart = 0,
          fieldCount = 0,
          isPrivate = isPriv,
          modId = g_nodes(d).modId,
          nodeId = d
        )
        gm_cStructCount = gm_cStructCount + 1
        gm_newDefs(gm_newDefCount) = d
        gm_newDefCount = gm_newDefCount + 1
      End If
    End If

    If dk = NdFuncDef Then
      If g_nodes(d).genCount > 0 Then
        GmValidateGenParams(g_nodes(d).genStart, g_nodes(d).genCount, g_nodes(d).intVal, g_nodes(d).lenVal)
        gm_funcTmpls(gm_funcTmplCount) = GmFuncTmpl With (
          nodeId = d,
          namePos = g_nodes(d).intVal,
          nameLen = g_nodes(d).lenVal,
          genStart = g_nodes(d).genStart,
          genCount = g_nodes(d).genCount,
          isPrivate = isPriv,
          isAsync = g_nodes(d).flags,
          modId = g_nodes(d).modId
        )
        gm_funcTmplCount = gm_funcTmplCount + 1
      Else
        gm_newDefs(gm_newDefCount) = d
        gm_newDefCount = gm_newDefCount + 1
      End If
    End If

    If dk = NdOpDef Then
      If g_nodes(d).genCount > 0 Then
        GmValidateGenParams(g_nodes(d).genStart, g_nodes(d).genCount, 0, 0)
        gm_opTmpls(gm_opTmplCount) = GmOpTmpl With (
          nodeId = d,
          opTok = g_nodes(d).intVal,
          genStart = g_nodes(d).genStart,
          genCount = g_nodes(d).genCount,
          isPrivate = isPriv,
          modId = g_nodes(d).modId
        )
        gm_opTmplCount = gm_opTmplCount + 1
      Else
        gm_newDefs(gm_newDefCount) = d
        gm_newDefCount = gm_newDefCount + 1
      End If
    End If

    If (dk = NdGlobalVar) Or (dk = NdExternDecl) Or (dk = NdTestDef) Then
      gm_newDefs(gm_newDefCount) = d
      gm_newDefCount = gm_newDefCount + 1
    End If
  End For

  Dim origConcreteCount As Integer = gm_newDefCount
  Dim origCStructCount As Integer = gm_cStructCount

  ' Pass 2a: Resolve concrete struct fields (instantiating any generic fields)
  For si = 0 To origCStructCount - 1 Do
    Dim sd As Integer = gm_cStructs(si).nodeId
    gm_curModId = g_nodes(sd).modId
    gm_altModId = -1
    Dim fs As Integer = gm_cFieldCount
    Dim fc As Integer = g_nodes(sd).childCount
    For j = 0 To fc - 1 Do
      Dim f As Integer = g_kids(g_nodes(sd).firstChild + j)
      Dim fty As GmType = GmApplyResolvedType(f)
      gm_cFields(gm_cFieldCount) = GmFieldInfo With ( namePos = g_nodes(f).intVal, nameLen = g_nodes(f).lenVal, ty = fty )
      gm_cFieldCount = gm_cFieldCount + 1
    End For
    gm_cStructs(si).fieldStart = fs
    gm_cStructs(si).fieldCount = fc
  End For

  ' Pass 2b: Resolve concrete globals, function signatures, externs, and operator signatures
  For i = 0 To origConcreteCount - 1 Do
    Dim d As Integer = gm_newDefs(i)
    Dim dk As Integer = g_nodes(d).kind
    gm_curModId = g_nodes(d).modId
    gm_altModId = -1
    Dim isPriv As Integer = 0
    If g_nodes(d).op = 2 Then
      isPriv = 1
    End If

    If dk = NdGlobalVar Then
      Dim gty As GmType = GmApplyResolvedType(d)
      gm_globals(gm_globalCount) = GmGlobalInfo With ( namePos = g_nodes(d).intVal, nameLen = g_nodes(d).lenVal, isPrivate = isPriv, modId = g_nodes(d).modId, ty = gty )
      gm_globalCount = gm_globalCount + 1
    End If

    If (dk = NdFuncDef) Or (dk = NdExternDecl) Then
      Dim pc As Integer = g_nodes(d).extraCount
      For j = 0 To pc - 1 Do
        Dim pn As Integer = g_kids(g_nodes(d).extraStart + j)
        GmApplyResolvedType(pn)
      End For
      Dim retTy As GmType = GmApplyResolvedType(d)
      Dim callRetTy As GmType = retTy
      Dim isAsy As Integer = 0
      If (dk = NdFuncDef) And (g_nodes(d).flags = 1) Then
        isAsy = 1
        callRetTy = GmType With ( kind = TyTask, innerKind = retTy.kind, arrLen = 0, namePos = retTy.namePos, nameLen = retTy.nameLen )
      End If
      Dim fnPriv As Integer = 0
      If (dk = NdFuncDef) And (g_nodes(d).op = 2) Then
        fnPriv = 1
      End If
      gm_cFuncs(gm_cFuncCount) = GmFuncInfo With (
        namePos = g_nodes(d).intVal,
        nameLen = g_nodes(d).lenVal,
        isPrivate = fnPriv,
        isAsync = isAsy,
        modId = g_nodes(d).modId,
        retTy = callRetTy
      )
      gm_cFuncCount = gm_cFuncCount + 1
    End If

    If dk = NdOpDef Then
      Dim pc As Integer = g_nodes(d).extraCount
      Dim p0Ty = GmMakePrim(TyNothing), p1Ty = GmMakePrim(TyNothing) As GmType
      For j = 0 To pc - 1 Do
        Dim pn As Integer = g_kids(g_nodes(d).extraStart + j)
        Dim pTy As GmType = GmApplyResolvedType(pn)
        If j = 0 Then
          p0Ty = pTy
        Else
          If j = 1 Then
            p1Ty = pTy
          End If
        End If
      End For
      Dim opRet As GmType = GmApplyResolvedType(d)
      gm_cOps(gm_cOpCount) = GmOpInfo With ( opTok = g_nodes(d).intVal, isPrivate = isPriv, modId = g_nodes(d).modId, paramCount = pc, p0 = p0Ty, p1 = p1Ty, retTy = opRet )
      gm_cOpCount = gm_cOpCount + 1
    End If
  End For

  ' Pass 3: Walk concrete global initializers, function bodies, operator bodies, and Test bodies
  For i = 0 To origConcreteCount - 1 Do
    Dim d As Integer = gm_newDefs(i)
    Dim dk As Integer = g_nodes(d).kind
    gm_curModId = g_nodes(d).modId
    gm_altModId = -1
    gm_localBase = 0
    gm_localCount = 0
    gm_withBase = 0
    gm_withDepth = 0

    If dk = NdGlobalVar Then
      If g_nodes(d).left >= 0 Then
        Dim initTy As GmType
        g_nodes(d).left = GmProcessExpr(g_nodes(d).left, 0, ByRef initTy)
      End If
    End If

    If (dk = NdFuncDef) Or (dk = NdOpDef) Then
      Dim pc As Integer = g_nodes(d).extraCount
      For j = 0 To pc - 1 Do
        Dim pn As Integer = g_kids(g_nodes(d).extraStart + j)
        Dim pTy As GmType With ( kind = g_nodes(pn).tyKind, innerKind = g_nodes(pn).tyInnerKind, arrLen = g_nodes(pn).tyArrLen, namePos = g_nodes(pn).tyNamePos, nameLen = g_nodes(pn).tyNameLen )
        If (pTy.kind = TyStruct) And (GmFindEnum(pTy.namePos, pTy.nameLen) >= 0) Then
          pTy.kind = TyEnum
        End If
        gm_locals(gm_localCount) = GmLocalSym With ( namePos = g_nodes(pn).intVal, nameLen = g_nodes(pn).lenVal, ty = pTy )
        gm_localCount = gm_localCount + 1
      End For
      For j = 0 To g_nodes(d).childCount - 1 Do
        g_kids(g_nodes(d).firstChild + j) = GmProcessStmt(g_kids(g_nodes(d).firstChild + j), 0)
      End For
    End If

    If dk = NdTestDef Then
      For j = 0 To g_nodes(d).childCount - 1 Do
        g_kids(g_nodes(d).firstChild + j) = GmProcessStmt(g_kids(g_nodes(d).firstChild + j), 0)
      End For
    End If
  End For

  ' Rebuild NdProgram top-level children with all concrete + monomorphized definitions
  g_nodes(progRoot).firstChild = g_kidCount
  g_nodes(progRoot).childCount = gm_newDefCount
  For i = 0 To gm_newDefCount - 1 Do
    PushKid(gm_newDefs(i))
  End For

  Return gm_errCount
End Function
