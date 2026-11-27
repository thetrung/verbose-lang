' VerBose Self-Hosting Compiler (verbose) - Semantic Type Analyzer (--ta)

Include "constants.vb"
Include "parser.vb"

Structure TaEnumInfo
  Dim namePos, nameLen, memStart, memCount, isPrivate, modId As Integer
End Structure

Structure TaEnumMem
  Dim namePos, nameLen As Integer
End Structure

Structure TaStructInfo
  Dim namePos, nameLen, fieldStart, fieldCount, isPrivate, modId As Integer
End Structure

Structure TaUnionInfo
  Dim namePos, nameLen, varStart, varCount, isPrivate, modId As Integer
End Structure

Structure TaUnionVar
  Dim namePos, nameLen, fieldStart, fieldCount As Integer
End Structure

Structure TaFieldInfo
  Dim namePos, nameLen As Integer, ty As TypeInfo
End Structure

Structure TaGlobalInfo
  Dim namePos, nameLen, isPrivate, modId, initNode As Integer, ty As TypeInfo
End Structure

Structure TaParamInfo
  Dim isByRef As Integer, ty As TypeInfo
End Structure

Structure TaFuncInfo
  Dim namePos, nameLen, isExtern, isVariadic, isPrivate, isAsync, modId, paramStart, paramCount As Integer
  Dim retTy, bodyRetTy As TypeInfo
End Structure

Structure TaLocalSym
  Dim namePos, nameLen As Integer, ty As TypeInfo
End Structure

Structure TaLabelInfo
  Dim namePos, nameLen, scopeId, deferCnt, totalDefers As Integer
End Structure

Structure TaOpInfo
  Dim opTok, isPrivate, modId, paramStart, paramCount As Integer
  Dim retTy As TypeInfo
End Structure

Structure TaFpSig
  Dim isSub, paramStart, paramCount As Integer
  Dim retTy As TypeInfo
End Structure

Private Static ta_enums() As TaEnumInfo, ta_enumMems() As TaEnumMem
Private Static ta_structs() As TaStructInfo, ta_fields() As TaFieldInfo
Private Static ta_unions() As TaUnionInfo, ta_unionVars() As TaUnionVar
Private Static ta_globals() As TaGlobalInfo, ta_funcs() As TaFuncInfo
Private Static ta_ops() As TaOpInfo
Private Static ta_params() As TaParamInfo, ta_locals() As TaLocalSym
Private Static ta_fpSigs() As TaFpSig, ta_fpParams() As TaParamInfo
Private Static ta_scopeParent() As Integer, ta_scopeDeferCnt() As Integer
Private Static ta_labels(128) As TaLabelInfo
Private Static ta_withTys(16) As TypeInfo
Private Static ta_enumCount, ta_enumMemCount, ta_structCount, ta_fieldCount, ta_unionCount, ta_unionVarCount, ta_globalCount, ta_funcCount, ta_opCount, ta_paramCount, ta_fpSigCount, ta_fpParamCount, ta_localCount, ta_labelCount, ta_withDepth, ta_loopDepth, ta_deferDepth, ta_curScopeId, ta_nextScopeId, ta_curDeferTotal, ta_curModId, ta_errCount As Integer = 0
Private Static ta_altModId = -1, ta_ptrSize = 8, ta_curFrameReported = 0 As Integer
Private Static ta_maxStackBytes = 65536, ta_curFrameBytes = 0 As Long
Private Static ta_curRetTy As TypeInfo

Public Sub TaSetTarget(targetId As Integer)
  If targetId = TgtArmM Then
    ta_ptrSize = 4
    ta_maxStackBytes = 4096
  Else
    ta_ptrSize = 8
    ta_maxStackBytes = 65536
  End If
End Sub

Public Sub TaSetMaxStack(bytes As Long)
  If bytes >= 0 Then
    ta_maxStackBytes = bytes
  End If
End Sub

Private Sub TaUnionLayout(uIdx As Integer, ByRef outMaxPaySz As Long, ByRef outMaxPayAl As Integer)
  outMaxPaySz = 0
  outMaxPayAl = 1
  If (uIdx < 0) Or (uIdx >= ta_unionCount) Then
    Return
  End If
  Dim vst = ta_unions(uIdx).varStart, vcnt = ta_unions(uIdx).varCount As Integer
  For vi = 0 To vcnt - 1 Do
    Dim fst = ta_unionVars(vst + vi).fieldStart, fcnt = ta_unionVars(vst + vi).fieldCount As Integer
    If fcnt > 0 Then
      Dim off As Long = 0
      Dim vAl As Integer = 1
      For fi = 0 To fcnt - 1 Do
        Dim fty As TypeInfo = ta_fields(fst + fi).ty
        Dim fal As Integer = TaTypeAlign(fty)
        If fal > vAl Then
          vAl = fal
        End If
        Dim rem As Long = off Mod Long(fal)
        If rem <> 0 Then
          off = off + (Long(fal) - rem)
        End If
        off = off + TaTypeSize(fty)
      End For
      Dim remEnd As Long = off Mod Long(vAl)
      If remEnd <> 0 Then
        off = off + (Long(vAl) - remEnd)
      End If
      If off > outMaxPaySz Then
        outMaxPaySz = off
      End If
      If vAl > outMaxPayAl Then
        outMaxPayAl = vAl
      End If
    End If
  End For
End Sub

Private Function TaTypeAlign(ty As TypeInfo) As Integer
  If ty.arrLen < 0 Then
    Return ta_ptrSize
  End If
  Match ty.kind With
    Case TyByte, TyBoolean Then Return 1
    Case TyShort Then Return 2
    Case TyInt, TySingle, TyEnum Then Return 4
    Case TyLong, TyDouble Then Return 8
    Case TyPointer, TyTask, TyFuncPtr Then Return ta_ptrSize
    Case TyArena Then Return 8
    Case TyStruct Then
      If (ty.typeIdx < 0) Or (ty.typeIdx >= ta_structCount) Then
        Return 8
      End If
      Dim maxAl As Integer = 1
      Dim fst As Integer = ta_structs(ty.typeIdx).fieldStart
      Dim fcnt As Integer = ta_structs(ty.typeIdx).fieldCount
      For i = 0 To fcnt - 1 Do
        Dim fal As Integer = TaTypeAlign(ta_fields(fst + i).ty)
        If fal > maxAl Then
          maxAl = fal
        End If
      End For
      Return maxAl
    Case TyUnion Then
      Dim maxSz As Long = 0
      Dim maxAl As Integer = 1
      TaUnionLayout(ty.typeIdx, ByRef maxSz, ByRef maxAl)
      If maxAl < 4 Then
        Return 4
      End If
      Return maxAl
    Case _ Then Return 4
  End Match
End Function

Private Function TaTypeSize(ty As TypeInfo) As Long
  If ty.arrLen < 0 Then
    Return Long(ta_ptrSize)
  End If
  Dim elemSz As Long = 4
  Match ty.kind With
    Case TyNothing Then elemSz = 0
    Case TyByte, TyBoolean Then elemSz = 1
    Case TyShort Then elemSz = 2
    Case TyInt, TySingle, TyEnum Then elemSz = 4
    Case TyLong, TyDouble Then elemSz = 8
    Case TyPointer, TyTask, TyFuncPtr Then elemSz = Long(ta_ptrSize)
    Case TyArena Then elemSz = Long(ta_ptrSize) + 24
    Case TyStruct Then
      If (ty.typeIdx < 0) Or (ty.typeIdx >= ta_structCount) Then
        elemSz = 8
      Else
        Dim off As Long = 0
        Dim maxAl As Long = 1
        Dim fst As Integer = ta_structs(ty.typeIdx).fieldStart
        Dim fcnt As Integer = ta_structs(ty.typeIdx).fieldCount
        For i = 0 To fcnt - 1 Do
          Dim fty As TypeInfo = ta_fields(fst + i).ty
          Dim fal As Long = Long(TaTypeAlign(fty))
          If fal > maxAl Then
            maxAl = fal
          End If
          Dim rem As Long = off Mod fal
          If rem <> 0 Then
            off = off + (fal - rem)
          End If
          off = off + TaTypeSize(fty)
        End For
        Dim remEnd As Long = off Mod maxAl
        If remEnd <> 0 Then
          off = off + (maxAl - remEnd)
        End If
        If off = 0 Then
          off = 1
        End If
        elemSz = off
      End If
    Case TyUnion Then
      Dim maxPaySz As Long = 0
      Dim maxPayAl As Integer = 1
      TaUnionLayout(ty.typeIdx, ByRef maxPaySz, ByRef maxPayAl)
      If maxPaySz = 0 Then
        elemSz = 4
      Else
        Dim pAl As Long = Long(maxPayAl)
        Dim off As Long = 4
        Dim rem1 As Long = off Mod pAl
        If rem1 <> 0 Then
          off = off + (pAl - rem1)
        End If
        Dim pSz As Long = maxPaySz
        Dim rem2 As Long = pSz Mod pAl
        If rem2 <> 0 Then
          pSz = pSz + (pAl - rem2)
        End If
        off = off + pSz
        Dim uAl As Long = pAl
        If uAl < 4 Then
          uAl = 4
        End If
        Dim rem3 As Long = off Mod uAl
        If rem3 <> 0 Then
          off = off + (uAl - rem3)
        End If
        elemSz = off
      End If
    Case _ Then elemSz = 4
  End Match
  If ty.arrLen > 0 Then
    Return elemSz * Long(ty.arrLen)
  End If
  Return elemSz
End Function

Private Sub TaAccountStackLocal(nPos, nLen As Integer, ty As TypeInfo)
  Dim sz As Long = TaTypeSize(ty)
  Dim alignedSz As Long = sz
  Dim rem As Long = alignedSz Mod 8
  If rem <> 0 Then
    alignedSz = alignedSz + (8 - rem)
  End If
  ta_curFrameBytes = ta_curFrameBytes + alignedSz
  If (ta_maxStackBytes > 0) And (sz > ta_maxStackBytes) Then
    Printf("Semantic Type Error: Local variable '")
    TaPrintSlice(nPos, nLen)
    Printf("' requires %lld bytes on the stack, exceeding max stack limit (%lld bytes); use HeapAlloc, ArenaAlloc, or Static instead\n", sz, ta_maxStackBytes)
    ta_errCount = ta_errCount + 1
    ta_curFrameReported = 1
  End If
End Sub

Private Sub TaCheckFrameBudget(kindLabel As Pointer, namePos, nameLen As Integer)
  If (ta_maxStackBytes > 0) And (ta_curFrameBytes > ta_maxStackBytes) And (ta_curFrameReported = 0) Then
    If nameLen > 0 Then
      Printf("Semantic Type Error: %s '", kindLabel)
      TaPrintSlice(namePos, nameLen)
      Printf("' stack frame requires %lld bytes, exceeding max stack limit (%lld bytes); move large locals to HeapAlloc, ArenaAlloc, or Static (or pass --max-stack)\n", ta_curFrameBytes, ta_maxStackBytes)
    Else
      Printf("Semantic Type Error: %s stack frame requires %lld bytes, exceeding max stack limit (%lld bytes); move large locals to HeapAlloc, ArenaAlloc, or Static (or pass --max-stack)\n", kindLabel, ta_curFrameBytes, ta_maxStackBytes)
    End If
    ta_errCount = ta_errCount + 1
  End If
End Sub

Private Sub TaPrintSlice(pos, slen As Integer)
  For i = 0 To slen - 1 Do
    Dim ch As Integer = g_srcPool(pos + i)
    Printf("%c", ch)
  End For
End Sub

Private Sub TaPrintType(ty As TypeInfo)
  Match ty.kind With
    Case TyNothing Then Printf("Nothing")
    Case TyInt Then Printf("Integer")
    Case TyByte Then Printf("Byte")
    Case TyShort Then Printf("Short")
    Case TyLong Then Printf("Long")
    Case TySingle Then Printf("Single")
    Case TyDouble Then Printf("Double")
    Case TyBoolean Then Printf("Boolean")
    Case TyPointer Then Printf("Pointer")
    Case TyArena Then Printf("Arena")
    Case TyTask Then
      If ty.innerKind = TyNothing Then
        Printf("Task")
      Else
        Printf("Task(")
        Dim innerT As TypeInfo With ( kind = ty.innerKind, typeIdx = ty.typeIdx, arrLen = 0, namePos = ty.namePos, nameLen = ty.nameLen, innerKind = 0 )
        TaPrintType(innerT)
        Printf(")")
      End If
    Case TyStruct, TyEnum, TyUnion Then TaPrintSlice(ty.namePos, ty.nameLen)
    Case TyFuncPtr Then
      If (ty.typeIdx >= 0) And (ty.typeIdx < ta_fpSigCount) Then
        Dim fs As TaFpSig = ta_fpSigs(ty.typeIdx)
        If fs.isSub = 1 Then
          Printf("SubPtr(")
        Else
          Printf("FuncPtr(")
        End If
        For fi = 0 To fs.paramCount - 1 Do
          If fi > 0 Then
            Printf(", ")
          End If
          Dim fp As TaParamInfo = ta_fpParams(fs.paramStart + fi)
          If fp.isByRef = 1 Then
            Printf("ByRef ")
          End If
          TaPrintType(fp.ty)
        End For
        Printf(")")
        If fs.isSub = 0 Then
          Printf(" As ")
          TaPrintType(fs.retTy)
        End If
      Else
        Printf("FuncPtr")
      End If
    Case _ Then Printf("Unknown")
  End Match
  If ty.arrLen > 0 Then
    Printf("(%d)", ty.arrLen)
  Else
    If ty.arrLen = -1 Then
      Printf("()")
    End If
  End If
End Sub

Private Function TaMakePrim(k As Integer) As TypeInfo
  Dim t As TypeInfo With ( kind = k, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
  Return t
End Function

Private Function TaFindEnum(pos, slen As Integer) As Integer
  For i = 0 To ta_enumCount - 1 Do
    If (ta_enums(i).isPrivate = 1) And ((ta_enums(i).modId = ta_curModId) Or ((ta_altModId >= 0) And (ta_enums(i).modId = ta_altModId))) And SlicesEqual(ta_enums(i).namePos, ta_enums(i).nameLen, pos, slen) Then
      Return i
    End If
  End For
  For i = 0 To ta_enumCount - 1 Do
    If (ta_enums(i).isPrivate = 0) And SlicesEqual(ta_enums(i).namePos, ta_enums(i).nameLen, pos, slen) Then
      Return i
    End If
  End For
  Return -1
End Function

Private Function TaHasPrivateEnumOther(pos, slen As Integer) As Boolean
  For i = 0 To ta_enumCount - 1 Do
    If (ta_enums(i).isPrivate = 1) And (ta_enums(i).modId <> ta_curModId) And SlicesEqual(ta_enums(i).namePos, ta_enums(i).nameLen, pos, slen) Then
      Return True
    End If
  End For
  Return False
End Function

Private Function TaHasEnumMem(eIdx, pos, slen As Integer) As Boolean
  Dim st = ta_enums(eIdx).memStart, cnt = ta_enums(eIdx).memCount, i = 0 As Integer
  While i < cnt Do
    If SlicesEqual(ta_enumMems(st + i).namePos, ta_enumMems(st + i).nameLen, pos, slen) Then
      Return True
    End If
    i = i + 1
  End While
  Return False
End Function

' Returns 0 if not found, 1 if uniquely found (sets outEnumIdx), >=2 if ambiguous
Private Function TaFindUnqualEnumMem(pos, slen As Integer, ByRef outEnumIdx As Integer) As Integer
  Dim matches As Integer = 0
  For i = 0 To ta_enumCount - 1 Do
    If (ta_enums(i).isPrivate = 0) Or (ta_enums(i).modId = ta_curModId) Then
      If TaHasEnumMem(i, pos, slen) Then
        outEnumIdx = i
        matches = matches + 1
      End If
    End If
  End For
  Return matches
End Function

Private Function TaFindUnion(pos, slen As Integer) As Integer
  For i = 0 To ta_unionCount - 1 Do
    If (ta_unions(i).isPrivate = 1) And ((ta_unions(i).modId = ta_curModId) Or ((ta_altModId >= 0) And (ta_unions(i).modId = ta_altModId))) And SlicesEqual(ta_unions(i).namePos, ta_unions(i).nameLen, pos, slen) Then
      Return i
    End If
  End For
  For i = 0 To ta_unionCount - 1 Do
    If (ta_unions(i).isPrivate = 0) And SlicesEqual(ta_unions(i).namePos, ta_unions(i).nameLen, pos, slen) Then
      Return i
    End If
  End For
  Return -1
End Function

Private Function TaHasPrivateUnionOther(pos, slen As Integer) As Boolean
  For i = 0 To ta_unionCount - 1 Do
    If (ta_unions(i).isPrivate = 1) And (ta_unions(i).modId <> ta_curModId) And SlicesEqual(ta_unions(i).namePos, ta_unions(i).nameLen, pos, slen) Then
      Return True
    End If
  End For
  Return False
End Function

Private Function TaFindUnionVariant(uIdx, vPos, vLen As Integer) As Integer
  Dim st = ta_unions(uIdx).varStart, cnt = ta_unions(uIdx).varCount As Integer
  For i = 0 To cnt - 1 Do
    If SlicesEqual(ta_unionVars(st + i).namePos, ta_unionVars(st + i).nameLen, vPos, vLen) Then
      Return i
    End If
  End For
  Return -1
End Function

Private Function TaFindUnqualUnionVariant(vPos, vLen As Integer, ByRef outUIdx, outVIdx As Integer) As Integer
  Dim matches As Integer = 0
  For i = 0 To ta_unionCount - 1 Do
    If (ta_unions(i).isPrivate = 0) Or (ta_unions(i).modId = ta_curModId) Then
      Dim vi As Integer = TaFindUnionVariant(i, vPos, vLen)
      If vi >= 0 Then
        outUIdx = i
        outVIdx = vi
        matches = matches + 1
      End If
    End If
  End For
  Return matches
End Function

Private Function TaFindStruct(pos, slen As Integer) As Integer
  For i = 0 To ta_structCount - 1 Do
    If (ta_structs(i).isPrivate = 1) And ((ta_structs(i).modId = ta_curModId) Or ((ta_altModId >= 0) And (ta_structs(i).modId = ta_altModId))) And SlicesEqual(ta_structs(i).namePos, ta_structs(i).nameLen, pos, slen) Then
      Return i
    End If
  End For
  For i = 0 To ta_structCount - 1 Do
    If (ta_structs(i).isPrivate = 0) And SlicesEqual(ta_structs(i).namePos, ta_structs(i).nameLen, pos, slen) Then
      Return i
    End If
  End For
  Return -1
End Function

Private Function TaHasPrivateStructOther(pos, slen As Integer) As Boolean
  For i = 0 To ta_structCount - 1 Do
    If (ta_structs(i).isPrivate = 1) And (ta_structs(i).modId <> ta_curModId) And SlicesEqual(ta_structs(i).namePos, ta_structs(i).nameLen, pos, slen) Then
      Return True
    End If
  End For
  Return False
End Function

Private Function TaFindStructField(sIdx, pos, slen As Integer, ByRef outTy As TypeInfo) As Integer
  Dim st = ta_structs(sIdx).fieldStart, cnt = ta_structs(sIdx).fieldCount, i = 0 As Integer
  While i < cnt Do
    If SlicesEqual(ta_fields(st + i).namePos, ta_fields(st + i).nameLen, pos, slen) Then
      outTy = ta_fields(st + i).ty
      Return i
    End If
    i = i + 1
  End While
  Return -1
End Function

Private Function TaFindGlobal(pos, slen As Integer) As Integer
  For i = 0 To ta_globalCount - 1 Do
    If (ta_globals(i).isPrivate = 1) And (ta_globals(i).modId = ta_curModId) And SlicesEqual(ta_globals(i).namePos, ta_globals(i).nameLen, pos, slen) Then
      Return i
    End If
  End For
  For i = 0 To ta_globalCount - 1 Do
    If (ta_globals(i).isPrivate = 0) And SlicesEqual(ta_globals(i).namePos, ta_globals(i).nameLen, pos, slen) Then
      Return i
    End If
  End For
  Return -1
End Function

Private Function TaHasPrivateGlobalOther(pos, slen As Integer) As Boolean
  For i = 0 To ta_globalCount - 1 Do
    If (ta_globals(i).isPrivate = 1) And (ta_globals(i).modId <> ta_curModId) And SlicesEqual(ta_globals(i).namePos, ta_globals(i).nameLen, pos, slen) Then
      Return True
    End If
  End For
  Return False
End Function

Private Function TaFindFunc(pos, slen As Integer) As Integer
  For i = 0 To ta_funcCount - 1 Do
    If (ta_funcs(i).isPrivate = 1) And (ta_funcs(i).modId = ta_curModId) And SlicesEqual(ta_funcs(i).namePos, ta_funcs(i).nameLen, pos, slen) Then
      Return i
    End If
  End For
  For i = 0 To ta_funcCount - 1 Do
    If (ta_funcs(i).isPrivate = 0) And SlicesEqual(ta_funcs(i).namePos, ta_funcs(i).nameLen, pos, slen) Then
      Return i
    End If
  End For
  Return -1
End Function

Private Function TaHasPrivateFuncOther(pos, slen As Integer) As Boolean
  For i = 0 To ta_funcCount - 1 Do
    If (ta_funcs(i).isPrivate = 1) And (ta_funcs(i).modId <> ta_curModId) And SlicesEqual(ta_funcs(i).namePos, ta_funcs(i).nameLen, pos, slen) Then
      Return True
    End If
  End For
  Return False
End Function

Private Function TaFindLocal(pos, slen As Integer) As Integer
  Dim i As Integer = ta_localCount - 1
  While i >= 0 Do
    If SlicesEqual(ta_locals(i).namePos, ta_locals(i).nameLen, pos, slen) Then
      Return i
    End If
    i = i - 1
  End While
  Return -1
End Function

Private Function TaLookupVar(pos, slen As Integer, ByRef outTy As TypeInfo) As Boolean
  Dim li As Integer = TaFindLocal(pos, slen)
  If li >= 0 Then
    outTy = ta_locals(li).ty
    Return True
  End If
  Dim gi As Integer = TaFindGlobal(pos, slen)
  If gi >= 0 Then
    outTy = ta_globals(gi).ty
    Return True
  End If
  Return False
End Function

Private Function TaRegisterFpSigFromNode(sigNodeId As Integer) As Integer
  If (sigNodeId < 0) Or (g_nodes(sigNodeId).kind <> NdFuncPtrType) Then
    Return -1
  End If
  Dim pc As Integer = g_nodes(sigNodeId).childCount
  Dim pBuf(64) As TaParamInfo
  For i = 0 To pc - 1 Do
    Dim pn As Integer = g_kids(g_nodes(sigNodeId).firstChild + i)
    Dim pty As TypeInfo = TaResolveTypeEx(g_nodes(pn).tyKind, g_nodes(pn).tyInnerKind, g_nodes(pn).tyArrLen, g_nodes(pn).tyNamePos, g_nodes(pn).tyNameLen)
    pBuf(i) = TaParamInfo With ( isByRef = g_nodes(pn).op, ty = pty )
  End For
  Dim rTy As TypeInfo = TaMakePrim(TyNothing)
  If (g_nodes(sigNodeId).op = 0) And (g_nodes(sigNodeId).left >= 0) Then
    Dim rn As Integer = g_nodes(sigNodeId).left
    rTy = TaResolveTypeEx(g_nodes(rn).tyKind, g_nodes(rn).tyInnerKind, 0, g_nodes(rn).tyNamePos, g_nodes(rn).tyNameLen)
  End If
  Dim pStart As Integer = ta_fpParamCount
  For i = 0 To pc - 1 Do
    If ta_fpParamCount < 4096 Then
      ta_fpParams(ta_fpParamCount) = pBuf(i)
      ta_fpParamCount = ta_fpParamCount + 1
    End If
  End For
  Dim idx As Integer = ta_fpSigCount
  If idx < 1024 Then
    ta_fpSigs(idx) = TaFpSig With ( isSub = g_nodes(sigNodeId).op, paramStart = pStart, paramCount = pc, retTy = rTy )
    ta_fpSigCount = ta_fpSigCount + 1
  End If
  Return idx
End Function

Private Function TaRegisterFpSigFromFunc(fnIdx As Integer) As Integer
  Dim fi As TaFuncInfo = ta_funcs(fnIdx)
  Dim pStart As Integer = ta_fpParamCount
  For i = 0 To fi.paramCount - 1 Do
    If ta_fpParamCount < 4096 Then
      ta_fpParams(ta_fpParamCount) = ta_params(fi.paramStart + i)
      ta_fpParamCount = ta_fpParamCount + 1
    End If
  End For
  Dim isSub As Integer = 0
  If fi.retTy.kind = TyNothing Then
    isSub = 1
  End If
  Dim idx As Integer = ta_fpSigCount
  If idx < 1024 Then
    ta_fpSigs(idx) = TaFpSig With ( isSub = isSub, paramStart = pStart, paramCount = fi.paramCount, retTy = fi.retTy )
    ta_fpSigCount = ta_fpSigCount + 1
  End If
  Return idx
End Function

Private Function TaFpSigsEqual(s1, s2 As Integer) As Boolean
  If s1 = s2 Then
    Return True
  End If
  If (s1 < 0) Or (s2 < 0) Or (s1 >= ta_fpSigCount) Or (s2 >= ta_fpSigCount) Then
    Return False
  End If
  Dim f1 As TaFpSig = ta_fpSigs(s1)
  Dim f2 As TaFpSig = ta_fpSigs(s2)
  If (f1.isSub <> f2.isSub) Or (f1.paramCount <> f2.paramCount) Then
    Return False
  End If
  If Not TaTypesEqual(f1.retTy, f2.retTy) Then
    Return False
  End If
  For i = 0 To f1.paramCount - 1 Do
    Dim p1 As TaParamInfo = ta_fpParams(f1.paramStart + i)
    Dim p2 As TaParamInfo = ta_fpParams(f2.paramStart + i)
    If p1.isByRef <> p2.isByRef Then
      Return False
    End If
    If Not TaTypesEqual(p1.ty, p2.ty) Then
      Return False
    End If
  End For
  Return True
End Function

Private Function TaResolveTypeEx(rawKind As Integer, rawInnerKind As Integer, arrLen As Integer, nPos As Integer, nLen As Integer) As TypeInfo
  Dim res As TypeInfo With (
    kind = rawKind,
    typeIdx = -1,
    arrLen = arrLen,
    namePos = nPos,
    nameLen = nLen,
    innerKind = rawInnerKind
  )
  If rawKind = TyFuncPtr Then
    res.typeIdx = TaRegisterFpSigFromNode(nPos)
    Return res
  End If
  If rawKind = TyStruct Then
    Dim eIdx As Integer = TaFindEnum(nPos, nLen)
    If eIdx >= 0 Then
      res.kind = TyEnum
      res.typeIdx = eIdx
    Else
      Dim uIdx As Integer = TaFindUnion(nPos, nLen)
      If uIdx >= 0 Then
        res.kind = TyUnion
        res.typeIdx = uIdx
      Else
        Dim sIdx As Integer = TaFindStruct(nPos, nLen)
        res.typeIdx = sIdx
      End If
    End If
  Else
    If (rawKind = TyTask) And (rawInnerKind = TyFuncPtr) Then
      res.typeIdx = TaRegisterFpSigFromNode(nPos)
    Else
      If (rawKind = TyTask) And (rawInnerKind = TyStruct) Then
        Dim eIdx As Integer = TaFindEnum(nPos, nLen)
        If eIdx >= 0 Then
          res.innerKind = TyEnum
          res.typeIdx = eIdx
        Else
          Dim uIdx As Integer = TaFindUnion(nPos, nLen)
          If uIdx >= 0 Then
            res.innerKind = TyUnion
            res.typeIdx = uIdx
          Else
            Dim sIdx As Integer = TaFindStruct(nPos, nLen)
            res.typeIdx = sIdx
          End If
        End If
      End If
    End If
  End If
  Return res
End Function

Private Function TaResolveType(rawKind As Integer, arrLen As Integer, nPos As Integer, nLen As Integer) As TypeInfo
  Return TaResolveTypeEx(rawKind, TyNothing, arrLen, nPos, nLen)
End Function

Private Sub TaValidateType(ty As TypeInfo)
  If ty.arrLen = -2 Then
    Printf(MSG_TA_ARRAY_SIZE_POS)
    ta_errCount = ta_errCount + 1
  End If
  If ((ty.kind = TyFuncPtr) Or ((ty.kind = TyTask) And (ty.innerKind = TyFuncPtr))) And (ty.typeIdx >= 0) And (ty.typeIdx < ta_fpSigCount) Then
    Dim fs As TaFpSig = ta_fpSigs(ty.typeIdx)
    For fi = 0 To fs.paramCount - 1 Do
      TaValidateType(ta_fpParams(fs.paramStart + fi).ty)
    End For
    If fs.isSub = 0 Then
      TaValidateType(fs.retTy)
    End If
    Return
  End If
  Dim badStruct As Boolean = (((ty.kind = TyStruct) Or (ty.kind = TyUnion)) And (ty.typeIdx < 0)) Or ((ty.kind = TyTask) And ((ty.innerKind = TyStruct) Or (ty.innerKind = TyUnion)) And (ty.typeIdx < 0))
  If badStruct Then
    If TaHasPrivateStructOther(ty.namePos, ty.nameLen) Or TaHasPrivateEnumOther(ty.namePos, ty.nameLen) Or TaHasPrivateUnionOther(ty.namePos, ty.nameLen) Then
      Printf(MSG_TA_PRIVATE_TYPE)
      TaPrintSlice(ty.namePos, ty.nameLen)
      Printf(MSG_TA_FROM_MOD_SUFFIX)
      ta_errCount = ta_errCount + 1
    Else
      Printf(MSG_TA_UNKNOWN_TYPE)
      TaPrintSlice(ty.namePos, ty.nameLen)
      Printf("'\n")
      ta_errCount = ta_errCount + 1
    End If
  End If
End Sub

Private Function TaIsStrLike(t As TypeInfo) As Boolean
  If (t.arrLen = 0) And (t.kind = TyPointer) Then
    Return True
  End If
  If (t.arrLen <> 0) And (t.kind = TyByte) Then
    Return True
  End If
  Return False
End Function

Private Function TaNormKind(k As Integer) As Integer
  If k = TyEnum Then
    Return TyInt
  End If
  Return k
End Function

Private Function TaIsIntType(ty As TypeInfo) As Boolean
  If ty.arrLen <> 0 Then
    Return False
  End If
  Dim k As Integer = TaNormKind(ty.kind)
  Return (k = TyByte) Or (k = TyShort) Or (k = TyInt) Or (k = TyLong)
End Function

Private Function TaIsFloatType(ty As TypeInfo) As Boolean
  If ty.arrLen <> 0 Then
    Return False
  End If
  Return (ty.kind = TySingle) Or (ty.kind = TyDouble)
End Function

Private Function TaBaseTypesEqual(t1 As TypeInfo, t2 As TypeInfo) As Boolean
  Dim k1 As Integer = TaNormKind(t1.kind)
  Dim k2 As Integer = TaNormKind(t2.kind)
  If k1 <> k2 Then
    Return False
  End If
  If (k1 = TyStruct) Or (k1 = TyUnion) Then
    Return t1.typeIdx = t2.typeIdx
  End If
  If k1 = TyFuncPtr Then
    Return TaFpSigsEqual(t1.typeIdx, t2.typeIdx)
  End If
  If k1 = TyTask Then
    Dim ik1 As Integer = TaNormKind(t1.innerKind)
    Dim ik2 As Integer = TaNormKind(t2.innerKind)
    If ik1 <> ik2 Then
      Return False
    End If
    If (ik1 = TyStruct) Or (ik1 = TyUnion) Then
      Return t1.typeIdx = t2.typeIdx
    End If
    If ik1 = TyFuncPtr Then
      Return TaFpSigsEqual(t1.typeIdx, t2.typeIdx)
    End If
    Return True
  End If
  Return True
End Function

Private Function TaTypesEqual(t1 As TypeInfo, t2 As TypeInfo) As Boolean
  If t1.arrLen <> t2.arrLen Then
    Return False
  End If
  Return TaBaseTypesEqual(t1, t2)
End Function

Private Function TaIsAssignable(target As TypeInfo, actual As TypeInfo, rhsNode As Integer) As Boolean
  If TaTypesEqual(target, actual) Then
    Return True
  End If
  If TaIsIntType(target) And TaIsIntType(actual) Then
    Return True
  End If
  If TaIsFloatType(target) And TaIsFloatType(actual) Then
    Return True
  End If
  ' Pointer <- Array, FuncPtr, or Integer
  If (target.arrLen = 0) And (target.kind = TyPointer) Then
    If actual.arrLen <> 0 Then
      Return True
    End If
    If actual.kind = TyFuncPtr Then
      Return True
    End If
    If TaIsIntType(actual) Then
      Return True
    End If
  End If
  ' Unsized slice <- Pointer, same-element Array, or literal 0
  If target.arrLen = -1 Then
    If (actual.arrLen = 0) And (actual.kind = TyPointer) Then
      Return True
    End If
    If (actual.arrLen <> 0) And TaBaseTypesEqual(target, actual) Then
      Return True
    End If
    If (rhsNode >= 0) And (g_nodes(rhsNode).kind = NdIntLit) And (g_nodes(rhsNode).longVal = 0) Then
      Return True
    End If
  End If
  ' Task(T) or FuncPtr <- literal 0 (null handle / null function pointer)
  If (target.arrLen = 0) And ((target.kind = TyTask) Or (target.kind = TyFuncPtr)) Then
    If (rhsNode >= 0) And (g_nodes(rhsNode).kind = NdIntLit) And (g_nodes(rhsNode).longVal = 0) Then
      Return True
    End If
  End If
  Return False
End Function

Private Function TaIsIntLitNode(id As Integer) As Boolean
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

Private Function TaIsFloatLitNode(id As Integer) As Boolean
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

Private Function TaOpParamMatchesLit(paramTy As TypeInfo, argTy As TypeInfo, argNode As Integer) As Boolean
  If TaTypesEqual(paramTy, argTy) Then
    Return True
  End If
  If TaIsFloatType(paramTy) And TaIsFloatLitNode(argNode) Then
    Return True
  End If
  If TaIsIntType(paramTy) And TaIsIntLitNode(argNode) Then
    Return True
  End If
  Return False
End Function

Private Function TaFindUnaryOp(opTok As Integer, t As TypeInfo) As Integer
  For i = 0 To ta_opCount - 1 Do
    If (ta_ops(i).isPrivate = 1) And (ta_ops(i).modId = ta_curModId) And (ta_ops(i).opTok = opTok) And (ta_ops(i).paramCount = 1) Then
      If TaTypesEqual(ta_params(ta_ops(i).paramStart).ty, t) Then
        Return i
      End If
    End If
  End For
  For i = 0 To ta_opCount - 1 Do
    If (ta_ops(i).isPrivate = 0) And (ta_ops(i).opTok = opTok) And (ta_ops(i).paramCount = 1) Then
      If TaTypesEqual(ta_params(ta_ops(i).paramStart).ty, t) Then
        Return i
      End If
    End If
  End For
  Return -1
End Function

Private Function TaFindBinOp(opTok As Integer, t1 As TypeInfo, e1 As Integer, t2 As TypeInfo, e2 As Integer) As Integer
  ' Pass 1: exact match (private in current module first, then public)
  For i = 0 To ta_opCount - 1 Do
    If (ta_ops(i).isPrivate = 1) And (ta_ops(i).modId = ta_curModId) And (ta_ops(i).opTok = opTok) And (ta_ops(i).paramCount = 2) Then
      Dim p0 As TypeInfo = ta_params(ta_ops(i).paramStart).ty
      Dim p1 As TypeInfo = ta_params(ta_ops(i).paramStart + 1).ty
      If TaTypesEqual(p0, t1) And TaTypesEqual(p1, t2) Then
        Return i
      End If
    End If
  End For
  For i = 0 To ta_opCount - 1 Do
    If (ta_ops(i).isPrivate = 0) And (ta_ops(i).opTok = opTok) And (ta_ops(i).paramCount = 2) Then
      Dim p0 As TypeInfo = ta_params(ta_ops(i).paramStart).ty
      Dim p1 As TypeInfo = ta_params(ta_ops(i).paramStart + 1).ty
      If TaTypesEqual(p0, t1) And TaTypesEqual(p1, t2) Then
        Return i
      End If
    End If
  End For
  ' Pass 2: contextual numeric literal match (e.g. Vec3 * 2.0 -> Operator *(Vec3, Single))
  For i = 0 To ta_opCount - 1 Do
    If (ta_ops(i).isPrivate = 1) And (ta_ops(i).modId = ta_curModId) And (ta_ops(i).opTok = opTok) And (ta_ops(i).paramCount = 2) Then
      Dim p0 As TypeInfo = ta_params(ta_ops(i).paramStart).ty
      Dim p1 As TypeInfo = ta_params(ta_ops(i).paramStart + 1).ty
      If TaOpParamMatchesLit(p0, t1, e1) And TaOpParamMatchesLit(p1, t2, e2) Then
        Return i
      End If
    End If
  End For
  For i = 0 To ta_opCount - 1 Do
    If (ta_ops(i).isPrivate = 0) And (ta_ops(i).opTok = opTok) And (ta_ops(i).paramCount = 2) Then
      Dim p0 As TypeInfo = ta_params(ta_ops(i).paramStart).ty
      Dim p1 As TypeInfo = ta_params(ta_ops(i).paramStart + 1).ty
      If TaOpParamMatchesLit(p0, t1, e1) And TaOpParamMatchesLit(p1, t2, e2) Then
        Return i
      End If
    End If
  End For
  Return -1
End Function

Private Function TaHasDupOp(opTok, isPriv, modId, ps, pc As Integer) As Boolean
  For i = 0 To ta_opCount - 1 Do
    Dim sameScope As Boolean = (ta_ops(i).isPrivate = 0) And (isPriv = 0)
    If (ta_ops(i).isPrivate = 1) And (isPriv = 1) And (ta_ops(i).modId = modId) Then
      sameScope = True
    End If
    If sameScope And (ta_ops(i).opTok = opTok) And (ta_ops(i).paramCount = pc) Then
      Dim allEq As Boolean = True
      For pi = 0 To pc - 1 Do
        If Not TaTypesEqual(ta_params(ta_ops(i).paramStart + pi).ty, ta_params(ps + pi).ty) Then
          allEq = False
        End If
      End For
      If allEq Then
        Return True
      End If
    End If
  End For
  Return False
End Function

Private Function TaIsVarIndexCall(callId As Integer) As Boolean
  If g_nodes(callId).childCount <> 1 Then
    Return False
  End If
  Dim aNode As Integer = g_kids(g_nodes(callId).firstChild)
  If g_nodes(aNode).op <> 0 Then
    Return False
  End If
  Dim nPos As Integer = g_nodes(callId).intVal
  Dim nLen As Integer = g_nodes(callId).lenVal
  Dim vTy As TypeInfo
  If Not TaLookupVar(nPos, nLen, ByRef vTy) Then
    Return False
  End If
  If (vTy.arrLen = 0) And (vTy.kind = TyFuncPtr) Then
    Return False
  End If
  If (vTy.arrLen <> 0) Or (vTy.kind = TyPointer) Then
    Return True
  End If
  If (TaFindFunc(nPos, nLen) < 0) And (TaFindStruct(nPos, nLen) < 0) And (TaFindUnion(nPos, nLen) < 0) Then
    Return True
  End If
  Return False
End Function

Private Function TaPeekExprIsFuncPtr(id As Integer) As Boolean
  If id < 0 Then
    Return False
  End If
  Dim k As Integer = g_nodes(id).kind
  If k = NdVar Then
    Dim vTy As TypeInfo
    If TaLookupVar(g_nodes(id).intVal, g_nodes(id).lenVal, ByRef vTy) Then
      Return (vTy.arrLen = 0) And (vTy.kind = TyFuncPtr)
    End If
    Return False
  End If
  If k = NdFieldAccess Then
    Dim baseId As Integer = g_nodes(id).left
    If baseId < 0 Then
      Return False
    End If
    Dim bTy As TypeInfo
    Dim haveBase As Boolean = False
    If g_nodes(baseId).kind = NdWithTarget Then
      If ta_withDepth > 0 Then
        bTy = ta_withTys(ta_withDepth - 1)
        haveBase = True
      End If
    Else
      If g_nodes(baseId).kind = NdVar Then
        haveBase = TaLookupVar(g_nodes(baseId).intVal, g_nodes(baseId).lenVal, ByRef bTy)
      End If
    End If
    If haveBase And (bTy.arrLen = 0) And (bTy.kind = TyStruct) And (bTy.typeIdx >= 0) Then
      Dim fTy As TypeInfo
      If TaFindStructField(bTy.typeIdx, g_nodes(id).intVal, g_nodes(id).lenVal, ByRef fTy) >= 0 Then
        Return (fTy.arrLen = 0) And (fTy.kind = TyFuncPtr)
      End If
    End If
    Return False
  End If
  If (k = NdCall) And TaIsVarIndexCall(id) Then
    Dim vTy As TypeInfo
    If TaLookupVar(g_nodes(id).intVal, g_nodes(id).lenVal, ByRef vTy) Then
      Return (vTy.arrLen <> 0) And (vTy.kind = TyFuncPtr)
    End If
    Return False
  End If
  If k = NdCall Then
    Dim fIdx As Integer = TaFindFunc(g_nodes(id).intVal, g_nodes(id).lenVal)
    If fIdx >= 0 Then
      Return (ta_funcs(fIdx).retTy.arrLen = 0) And (ta_funcs(fIdx).retTy.kind = TyFuncPtr)
    End If
  End If
  Return False
End Function

Private Function TaIsLValue(id As Integer) As Boolean
  If id < 0 Then
    Return False
  End If
  Dim k As Integer = g_nodes(id).kind
  If k = NdVar Then
    Dim tmpTy As TypeInfo
    If TaLookupVar(g_nodes(id).intVal, g_nodes(id).lenVal, ByRef tmpTy) Then
      Return True
    End If
    Dim dummyE = -1, dummyU = -1, dummyV = -1 As Integer
    If (TaFindEnum(g_nodes(id).intVal, g_nodes(id).lenVal) >= 0) Or (TaFindUnqualEnumMem(g_nodes(id).intVal, g_nodes(id).lenVal, ByRef dummyE) > 0) Or (TaFindUnion(g_nodes(id).intVal, g_nodes(id).lenVal) >= 0) Or (TaFindUnqualUnionVariant(g_nodes(id).intVal, g_nodes(id).lenVal, ByRef dummyU, ByRef dummyV) > 0) Then
      Return False
    End If
    Return True
  End If
  If k = NdIndexAccess Then
    If TaPeekExprIsFuncPtr(g_nodes(id).left) Then
      Return False
    End If
    Return True
  End If
  If (k = NdFieldAccess) Or (k = NdWithTarget) Then
    Return True
  End If
  If k = NdCall Then
    Return TaIsVarIndexCall(id)
  End If
  Return False
End Function

Private Function TaIsConstExpr(ty As TypeInfo, exprId As Integer) As Boolean
  If exprId < 0 Then
    Return True
  End If
  Dim k As Integer = g_nodes(exprId).kind
  ' Typed FuncPtr initialized to 0 (null) or AddressOf(non-Async function)
  If (ty.arrLen = 0) And (ty.kind = TyFuncPtr) Then
    If (k = NdIntLit) And (g_nodes(exprId).longVal = 0) Then
      Return True
    End If
    If k = NdAddressOf Then
      Dim tgt As Integer = g_nodes(exprId).left
      If (tgt >= 0) And (g_nodes(tgt).kind = NdVar) Then
        Dim fi As Integer = TaFindFunc(g_nodes(tgt).intVal, g_nodes(tgt).lenVal)
        If (fi >= 0) And (ta_funcs(fi).isAsync = 0) Then
          Return True
        End If
      End If
    End If
    Return False
  End If
  ' Pointer or unsized slice initialized to integer address / 0, StringLit, or AddressOf(func/global)
  If ((ty.arrLen = 0) And (ty.kind = TyPointer)) Or (ty.arrLen = -1) Then
    If (k = NdIntLit) And ((g_nodes(exprId).longVal = 0) Or ((ty.arrLen = 0) And (ty.kind = TyPointer))) Then
      Return True
    End If
    If (ty.arrLen = 0) And (k = NdStrLit) Then
      Return True
    End If
    If (ty.arrLen = 0) And (k = NdAddressOf) Then
      Dim tgt As Integer = g_nodes(exprId).left
      If (tgt >= 0) And (g_nodes(tgt).kind = NdVar) Then
        If (TaFindFunc(g_nodes(tgt).intVal, g_nodes(tgt).lenVal) >= 0) Or (TaFindGlobal(g_nodes(tgt).intVal, g_nodes(tgt).lenVal) >= 0) Then
          Return True
        End If
      End If
    End If
    Return False
  End If
  If (ty.arrLen = 0) And (ty.kind = TyTask) Then
    Return (k = NdIntLit) And (g_nodes(exprId).longVal = 0)
  End If
  If TaIsIntType(ty) Then
    If TaIsIntLitNode(exprId) Then
      Return True
    End If
    If k = NdFieldAccess Then
      Dim baseId As Integer = g_nodes(exprId).left
      If (baseId >= 0) And (g_nodes(baseId).kind = NdVar) Then
        Dim eIdx As Integer = TaFindEnum(g_nodes(baseId).intVal, g_nodes(baseId).lenVal)
        If (eIdx >= 0) And TaHasEnumMem(eIdx, g_nodes(exprId).intVal, g_nodes(exprId).lenVal) Then
          Return True
        End If
      End If
    End If
    If k = NdVar Then
      Dim dummyTy As TypeInfo
      If Not TaLookupVar(g_nodes(exprId).intVal, g_nodes(exprId).lenVal, ByRef dummyTy) Then
        Dim ueIdx As Integer = -1
        If TaFindUnqualEnumMem(g_nodes(exprId).intVal, g_nodes(exprId).lenVal, ByRef ueIdx) = 1 Then
          Return True
        End If
      End If
    End If
    Return False
  End If
  If TaIsFloatType(ty) Then
    Return TaIsFloatLitNode(exprId)
  End If
  If (ty.arrLen = 0) And (ty.kind = TyBoolean) Then
    Return k = NdBoolLit
  End If
  If (ty.arrLen > 0) And (k = NdArrayLit) Then
    If g_nodes(exprId).childCount > ty.arrLen Then
      Printf("Semantic Type Error: Static array initializer has %d elements, exceeding size %d\n", g_nodes(exprId).childCount, ty.arrLen)
      ta_errCount = ta_errCount + 1
      Return True
    End If
    Dim elemTy As TypeInfo With ( kind = ty.kind, typeIdx = ty.typeIdx, arrLen = 0, namePos = ty.namePos, nameLen = ty.nameLen, innerKind = ty.innerKind )
    Dim i As Integer = 0
    While i < g_nodes(exprId).childCount Do
      If Not TaIsConstExpr(elemTy, g_kids(g_nodes(exprId).firstChild + i)) Then
        Return False
      End If
      i = i + 1
    End While
    Return True
  End If
  If (ty.arrLen = 0) And (ty.kind = TyStruct) And (ty.typeIdx >= 0) And (k = NdStructInit) Then
    Dim initSIdx As Integer = TaFindStruct(g_nodes(exprId).intVal, g_nodes(exprId).lenVal)
    If initSIdx <> ty.typeIdx Then
      Printf("Semantic Type Error: Static struct type mismatch\n")
      ta_errCount = ta_errCount + 1
      Return True
    End If
    For i = 0 To g_nodes(exprId).childCount - 1 Do
      Dim fi As Integer = g_kids(g_nodes(exprId).firstChild + i)
      Dim fTy As TypeInfo
      Dim fIdx As Integer = TaFindStructField(ty.typeIdx, g_nodes(fi).intVal, g_nodes(fi).lenVal, ByRef fTy)
      If fIdx >= 0 Then
        If Not TaIsConstExpr(fTy, g_nodes(fi).left) Then
          Return False
        End If
      End If
    End For
    Return True
  End If
  Return False
End Function

Private Function TaCheckArenaArg(fnName As Pointer, mode As Integer, argExpr As Integer) As Boolean
  If mode <> 1 Then
    Printf("Semantic Type Error: %s expects ByRef arena\n", fnName)
    ta_errCount = ta_errCount + 1
    Return False
  End If
  If Not TaIsLValue(argExpr) Then
    Printf("Semantic Type Error: ByRef argument must be a variable, struct field, or array element\n")
    ta_errCount = ta_errCount + 1
    Return False
  End If
  Dim dt As TypeInfo = TaTypeOfExpr(argExpr)
  If (dt.arrLen <> 0) Or (dt.kind <> TyArena) Then
    Printf("Semantic Type Error: %s argument must be an Arena variable\n", fnName)
    ta_errCount = ta_errCount + 1
    Return False
  End If
  Return True
End Function

Private Function TaTryEvalConstInt(exprId As Integer, ByRef outVal As Long) As Boolean
  If exprId < 0 Then
    Return False
  End If
  Dim k As Integer = g_nodes(exprId).kind
  If k = NdIntLit Then
    outVal = g_nodes(exprId).longVal
    Return True
  End If
  If k = NdNeg Then
    Dim subVal As Long = 0
    If TaTryEvalConstInt(g_nodes(exprId).left, ByRef subVal) Then
      outVal = -subVal
      Return True
    End If
    Return False
  End If
  If k = NdBinOp Then
    Dim lv = Long(0), rv = Long(0) As Long
    If TaTryEvalConstInt(g_nodes(exprId).left, ByRef lv) And TaTryEvalConstInt(g_nodes(exprId).right, ByRef rv) Then
      Match g_nodes(exprId).op With
        Case TkPlus Then
          outVal = lv + rv
          Return True
        Case TkMinus Then
          outVal = lv - rv
          Return True
        Case TkStar Then
          outVal = lv * rv
          Return True
      End Match
    End If
  End If
  Return False
End Function

Private Function TaCheckIndex(baseTy As TypeInfo, idxExpr As Integer) As TypeInfo
  Dim idxTy As TypeInfo = TaTypeOfExpr(idxExpr)
  If Not TaIsIntType(idxTy) Then
    Printf("Semantic Type Error: Array index must be an integer type\n")
    ta_errCount = ta_errCount + 1
  End If
  If baseTy.arrLen > 0 Then
    Dim cIdx As Long = 0
    If TaTryEvalConstInt(idxExpr, ByRef cIdx) Then
      If (cIdx < 0) Or (cIdx >= Long(baseTy.arrLen)) Then
        Printf("Semantic Type Error: Array index %lld out of bounds for fixed array of length %d (valid: 0..%d)\n", cIdx, baseTy.arrLen, baseTy.arrLen - 1)
        ta_errCount = ta_errCount + 1
      End If
    End If
  End If
  If baseTy.arrLen <> 0 Then
    Dim elemTy As TypeInfo With ( kind = baseTy.kind, typeIdx = baseTy.typeIdx, arrLen = 0, namePos = baseTy.namePos, nameLen = baseTy.nameLen, innerKind = baseTy.innerKind )
    Return elemTy
  End If
  If baseTy.kind = TyPointer Then
    Return TaMakePrim(TyByte)
  End If
  Printf("Semantic Type Error: Cannot index non-array, non-pointer expression\n")
  ta_errCount = ta_errCount + 1
  Return TaMakePrim(TyInt)
End Function

Private Function TaTypeOfCall(id As Integer) As TypeInfo
  Dim nPos As Integer = g_nodes(id).intVal
  Dim nLen As Integer = g_nodes(id).lenVal
  Dim aStart As Integer = g_nodes(id).firstChild
  Dim aCount As Integer = g_nodes(id).childCount

  ' IsNull(p)
  If SliceEqualsStr(nPos, nLen, "IsNull", 6) Then
    If aCount <> 1 Then
      Printf("Semantic Type Error: IsNull() expects exactly one ByVal pointer/slice argument\n")
      ta_errCount = ta_errCount + 1
      Return TaMakePrim(TyBoolean)
    End If
    Dim a0 As Integer = g_kids(aStart)
    If g_nodes(a0).op = 1 Then
      Printf("Semantic Type Error: IsNull() expects exactly one ByVal pointer/slice argument\n")
      ta_errCount = ta_errCount + 1
      Return TaMakePrim(TyBoolean)
    End If
    Dim pt As TypeInfo = TaTypeOfExpr(g_nodes(a0).left)
    If (pt.arrLen = 0) And (pt.kind <> TyPointer) And (pt.kind <> TyTask) And (pt.kind <> TyFuncPtr) Then
      Printf("Semantic Type Error: IsNull expects a Pointer or slice argument\n")
      ta_errCount = ta_errCount + 1
    End If
    Return TaMakePrim(TyBoolean)
  End If

  ' Len(arr)
  If SliceEqualsStr(nPos, nLen, "Len", 3) Then
    If aCount <> 1 Then
      Printf("Semantic Type Error: Len() expects exactly one ByVal fixed-size array argument\n")
      ta_errCount = ta_errCount + 1
      Return TaMakePrim(TyInt)
    End If
    Dim a0 As Integer = g_kids(aStart)
    If (g_nodes(a0).op = 1) Or (Not TaIsLValue(g_nodes(a0).left)) Then
      Printf("Semantic Type Error: Len() requires a fixed-size array\n")
      ta_errCount = ta_errCount + 1
      Return TaMakePrim(TyInt)
    End If
    Dim aty As TypeInfo = TaTypeOfExpr(g_nodes(a0).left)
    If aty.arrLen <= 0 Then
      Printf("Semantic Type Error: Len() requires a fixed-size array\n")
      ta_errCount = ta_errCount + 1
    End If
    Return TaMakePrim(TyInt)
  End If

  ' ArenaCreate(cap)
  If SliceEqualsStr(nPos, nLen, "ArenaCreate", 11) Then
    If aCount <> 1 Then
      Printf("Semantic Type Error: ArenaCreate() expects exactly one ByVal capacity argument\n")
      ta_errCount = ta_errCount + 1
      Return TaMakePrim(TyArena)
    End If
    Dim a0 As Integer = g_kids(aStart)
    If g_nodes(a0).op = 1 Then
      Printf("Semantic Type Error: ArenaCreate() expects exactly one ByVal capacity argument\n")
      ta_errCount = ta_errCount + 1
      Return TaMakePrim(TyArena)
    End If
    Dim ct As TypeInfo = TaTypeOfExpr(g_nodes(a0).left)
    If Not TaIsIntType(ct) Then
      Printf("Semantic Type Error: ArenaCreate capacity must be an integer type\n")
      ta_errCount = ta_errCount + 1
    End If
    Return TaMakePrim(TyArena)
  End If

  ' ArenaFromBuffer(ByRef buf, cap)
  If SliceEqualsStr(nPos, nLen, "ArenaFromBuffer", 15) Then
    If aCount <> 2 Then
      Printf("Semantic Type Error: ArenaFromBuffer() expects (ByRef buffer, capacity)\n")
      ta_errCount = ta_errCount + 1
      Return TaMakePrim(TyArena)
    End If
    Dim a0 As Integer = g_kids(aStart)
    Dim a1 As Integer = g_kids(aStart + 1)
    If g_nodes(a0).op <> 1 Then
      Printf("Semantic Type Error: ArenaFromBuffer expects ByRef buffer as argument 1\n")
      ta_errCount = ta_errCount + 1
    Else
      If Not TaIsLValue(g_nodes(a0).left) Then
        Printf("Semantic Type Error: ByRef argument must be a variable, struct field, or array element\n")
        ta_errCount = ta_errCount + 1
      Else
        TaTypeOfExpr(g_nodes(a0).left)
      End If
    End If
    Dim ct As TypeInfo = TaTypeOfExpr(g_nodes(a1).left)
    If Not TaIsIntType(ct) Then
      Printf("Semantic Type Error: ArenaFromBuffer capacity must be an integer type\n")
      ta_errCount = ta_errCount + 1
    End If
    Return TaMakePrim(TyArena)
  End If

  ' ArenaAllocBytes(ByRef arena, bytes)
  If SliceEqualsStr(nPos, nLen, "ArenaAllocBytes", 15) Then
    If aCount <> 2 Then
      Printf("Semantic Type Error: ArenaAllocBytes() expects (ByRef arena, byteCount)\n")
      ta_errCount = ta_errCount + 1
      Return TaMakePrim(TyPointer)
    End If
    Dim a0 As Integer = g_kids(aStart)
    Dim a1 As Integer = g_kids(aStart + 1)
    TaCheckArenaArg("ArenaAllocBytes", g_nodes(a0).op, g_nodes(a0).left)
    Dim bt As TypeInfo = TaTypeOfExpr(g_nodes(a1).left)
    If Not TaIsIntType(bt) Then
      Printf("Semantic Type Error: ArenaAllocBytes size must be an integer type\n")
      ta_errCount = ta_errCount + 1
    End If
    Return TaMakePrim(TyPointer)
  End If

  ' ArenaReset / ArenaDestroy
  If SliceEqualsStr(nPos, nLen, "ArenaReset", 10) Then
    If aCount <> 1 Then
      Printf("Semantic Type Error: ArenaReset() expects (ByRef arena)\n")
      ta_errCount = ta_errCount + 1
    Else
      Dim a0 As Integer = g_kids(aStart)
      TaCheckArenaArg("ArenaReset", g_nodes(a0).op, g_nodes(a0).left)
    End If
    Return TaMakePrim(TyNothing)
  End If

  If SliceEqualsStr(nPos, nLen, "ArenaDestroy", 12) Then
    If aCount <> 1 Then
      Printf("Semantic Type Error: ArenaDestroy() expects (ByRef arena)\n")
      ta_errCount = ta_errCount + 1
    Else
      Dim a0 As Integer = g_kids(aStart)
      TaCheckArenaArg("ArenaDestroy", g_nodes(a0).op, g_nodes(a0).left)
    End If
    Return TaMakePrim(TyNothing)
  End If

  ' ArenaMark / ArenaUsed / ArenaRemaining / ArenaCapacity
  If SliceEqualsStr(nPos, nLen, "ArenaMark", 9) Then
    If aCount <> 1 Then
      Printf("Semantic Type Error: ArenaMark() expects (ByRef arena)\n")
      ta_errCount = ta_errCount + 1
    Else
      Dim a0 As Integer = g_kids(aStart)
      TaCheckArenaArg("ArenaMark", g_nodes(a0).op, g_nodes(a0).left)
    End If
    Return TaMakePrim(TyLong)
  End If

  If SliceEqualsStr(nPos, nLen, "ArenaUsed", 9) Then
    If aCount <> 1 Then
      Printf("Semantic Type Error: ArenaUsed() expects (ByRef arena)\n")
      ta_errCount = ta_errCount + 1
    Else
      Dim a0 As Integer = g_kids(aStart)
      TaCheckArenaArg("ArenaUsed", g_nodes(a0).op, g_nodes(a0).left)
    End If
    Return TaMakePrim(TyLong)
  End If

  If SliceEqualsStr(nPos, nLen, "ArenaRemaining", 14) Then
    If aCount <> 1 Then
      Printf("Semantic Type Error: ArenaRemaining() expects (ByRef arena)\n")
      ta_errCount = ta_errCount + 1
    Else
      Dim a0 As Integer = g_kids(aStart)
      TaCheckArenaArg("ArenaRemaining", g_nodes(a0).op, g_nodes(a0).left)
    End If
    Return TaMakePrim(TyLong)
  End If

  If SliceEqualsStr(nPos, nLen, "ArenaCapacity", 13) Then
    If aCount <> 1 Then
      Printf("Semantic Type Error: ArenaCapacity() expects (ByRef arena)\n")
      ta_errCount = ta_errCount + 1
    Else
      Dim a0 As Integer = g_kids(aStart)
      TaCheckArenaArg("ArenaCapacity", g_nodes(a0).op, g_nodes(a0).left)
    End If
    Return TaMakePrim(TyLong)
  End If

  ' ArenaRewind(ByRef arena, mark)
  If SliceEqualsStr(nPos, nLen, "ArenaRewind", 11) Then
    If aCount <> 2 Then
      Printf("Semantic Type Error: ArenaRewind() expects (ByRef arena, mark)\n")
      ta_errCount = ta_errCount + 1
    Else
      Dim a0 As Integer = g_kids(aStart)
      Dim a1 As Integer = g_kids(aStart + 1)
      TaCheckArenaArg("ArenaRewind", g_nodes(a0).op, g_nodes(a0).left)
      Dim mt As TypeInfo = TaTypeOfExpr(g_nodes(a1).left)
      If Not TaIsIntType(mt) Then
        Printf("Semantic Type Error: ArenaRewind mark must be an integer type\n")
        ta_errCount = ta_errCount + 1
      End If
    End If
    Return TaMakePrim(TyNothing)
  End If

  ' HeapAllocBytes(size)
  If SliceEqualsStr(nPos, nLen, "HeapAllocBytes", 14) Then
    If aCount <> 1 Then
      Printf("Semantic Type Error: HeapAllocBytes() expects exactly one ByVal byte-count argument\n")
      ta_errCount = ta_errCount + 1
    Else
      Dim a0 As Integer = g_kids(aStart)
      Dim st As TypeInfo = TaTypeOfExpr(g_nodes(a0).left)
      If Not TaIsIntType(st) Then
        Printf("Semantic Type Error: HeapAllocBytes size must be an integer type\n")
        ta_errCount = ta_errCount + 1
      End If
    End If
    Return TaMakePrim(TyPointer)
  End If

  ' HeapFree(ptr)
  If SliceEqualsStr(nPos, nLen, "HeapFree", 8) Then
    If aCount <> 1 Then
      Printf("Semantic Type Error: HeapFree() expects exactly one ByVal pointer/array argument\n")
      ta_errCount = ta_errCount + 1
    Else
      Dim a0 As Integer = g_kids(aStart)
      Dim pt As TypeInfo = TaTypeOfExpr(g_nodes(a0).left)
      If (pt.arrLen = 0) And (pt.kind <> TyPointer) Then
        Printf("Semantic Type Error: HeapFree expects a Pointer or heap array\n")
        ta_errCount = ta_errCount + 1
      End If
    End If
    Return TaMakePrim(TyNothing)
  End If

  ' Printf(...)
  If SliceEqualsStr(nPos, nLen, "Printf", 6) Then
    For i = 0 To aCount - 1 Do
      Dim aNode As Integer = g_kids(aStart + i)
      If g_nodes(aNode).op = 1 Then
        Printf("Semantic Type Error: Printf does not accept ByRef arguments\n")
        ta_errCount = ta_errCount + 1
      End If
      TaTypeOfExpr(g_nodes(aNode).left)
    End For
    Return TaMakePrim(TyInt)
  End If

  ' Function pointer variable call parsed as Call: fp(args...)
  Dim fpVarTy As TypeInfo
  If TaLookupVar(nPos, nLen, ByRef fpVarTy) And (fpVarTy.arrLen = 0) And (fpVarTy.kind = TyFuncPtr) Then
    Return TaCheckIndirectCall(fpVarTy, aStart, aCount, 1)
  End If

  ' Array / pointer variable indexing parsed as Call: var(idx)
  If TaIsVarIndexCall(id) Then
    Dim vTy As TypeInfo
    TaLookupVar(nPos, nLen, ByRef vTy)
    Dim idxNode As Integer = g_nodes(g_kids(aStart)).left
    Return TaCheckIndex(vTy, idxNode)
  End If

  ' Positional struct constructor: StructName(arg0, ...)
  Dim sIdx As Integer = TaFindStruct(nPos, nLen)
  If sIdx >= 0 Then
    Dim fCnt As Integer = ta_structs(sIdx).fieldCount
    Dim fStart As Integer = ta_structs(sIdx).fieldStart
    If aCount > fCnt Then
      Printf("Semantic Type Error: Too many arguments to struct constructor '")
      TaPrintSlice(nPos, nLen)
      Printf("': expected at most %d, got %d\n", fCnt, aCount)
      ta_errCount = ta_errCount + 1
    End If
    Dim ci As Integer = 0
    While (ci < aCount) And (ci < fCnt) Do
      Dim aNode As Integer = g_kids(aStart + ci)
      If g_nodes(aNode).op = 1 Then
        Printf("Semantic Type Error: Struct constructor does not accept ByRef arguments\n")
        ta_errCount = ta_errCount + 1
      End If
      Dim fTy As TypeInfo = ta_fields(fStart + ci).ty
      Dim argTy As TypeInfo = TaTypeOfExpr(g_nodes(aNode).left)
      If Not TaIsAssignable(fTy, argTy, g_nodes(aNode).left) Then
        Printf("Semantic Type Error: Type mismatch for field '")
        TaPrintSlice(ta_fields(fStart + ci).namePos, ta_fields(fStart + ci).nameLen)
        Printf("' in '")
        TaPrintSlice(nPos, nLen)
        Printf("' constructor\n")
        ta_errCount = ta_errCount + 1
      End If
      ci = ci + 1
    End While
    Dim res As TypeInfo With ( kind = TyStruct, typeIdx = sIdx, arrLen = 0, namePos = nPos, nameLen = nLen )
    Return res
  End If

  ' User Function / Sub / ExternDecl
  Dim fnIdx As Integer = TaFindFunc(nPos, nLen)
  If fnIdx >= 0 Then
    Dim expectedN As Integer = ta_funcs(fnIdx).paramCount
    Dim isVar As Integer = ta_funcs(fnIdx).isVariadic
    If ((isVar = 0) And (aCount <> expectedN)) Or ((isVar = 1) And (aCount < expectedN)) Then
      Printf("Semantic Type Error: Function '")
      TaPrintSlice(nPos, nLen)
      If isVar = 1 Then
        Printf("' expects at least %d argument(s), got %d\n", expectedN, aCount)
      Else
        Printf("' expects %d argument(s), got %d\n", expectedN, aCount)
      End If
      ta_errCount = ta_errCount + 1
    End If
    Dim pStart As Integer = ta_funcs(fnIdx).paramStart
    For ai = 0 To aCount - 1 Do
      Dim aNode As Integer = g_kids(aStart + ai)
      Dim argMode As Integer = g_nodes(aNode).op
      Dim argExpr As Integer = g_nodes(aNode).left
      Dim actualTy As TypeInfo = TaTypeOfExpr(argExpr)
      If ai < expectedN Then
        Dim pInfo As TaParamInfo = ta_params(pStart + ai)
        If pInfo.isByRef = 1 Then
          If argMode <> 1 Then
            Printf("Semantic Type Error: Argument %d of '", ai + 1)
            TaPrintSlice(nPos, nLen)
            Printf("' expects ByRef at call site\n")
            ta_errCount = ta_errCount + 1
          Else
            If Not TaIsLValue(argExpr) Then
              Printf("Semantic Type Error: ByRef argument must be a variable, struct field, or array element\n")
              ta_errCount = ta_errCount + 1
            End If
          End If
          If Not TaIsAssignable(pInfo.ty, actualTy, argExpr) Then
            Printf("Semantic Type Error: Argument %d of '", ai + 1)
            TaPrintSlice(nPos, nLen)
            Printf("' type mismatch: expected ")
            TaPrintType(pInfo.ty)
            Printf(", got ")
            TaPrintType(actualTy)
            Printf("\n")
            ta_errCount = ta_errCount + 1
          End If
        Else
          If argMode = 1 Then
            Printf("Semantic Type Error: Argument %d of '", ai + 1)
            TaPrintSlice(nPos, nLen)
            Printf("' is ByVal and cannot be passed ByRef\n")
            ta_errCount = ta_errCount + 1
          End If
          If Not TaIsAssignable(pInfo.ty, actualTy, argExpr) Then
            Printf("Semantic Type Error: Argument %d of '", ai + 1)
            TaPrintSlice(nPos, nLen)
            Printf("' type mismatch: expected ")
            TaPrintType(pInfo.ty)
            Printf(", got ")
            TaPrintType(actualTy)
            Printf("\n")
            ta_errCount = ta_errCount + 1
          End If
        End If
      Else
        If (isVar = 1) And (argMode = 1) Then
          If Not TaIsLValue(argExpr) Then
            Printf("Semantic Type Error: ByRef argument must be a variable, struct field, or array element\n")
            ta_errCount = ta_errCount + 1
          End If
        Else
          If argMode = 1 Then
            Printf("Semantic Type Error: Argument %d of '", ai + 1)
            TaPrintSlice(nPos, nLen)
            Printf("' is ByVal and cannot be passed ByRef\n")
            ta_errCount = ta_errCount + 1
          End If
        End If
      End If
    End For
    Return ta_funcs(fnIdx).retTy
  End If

  ' Built-in C runtime defaults: Malloc, Fopen, Calloc, Realloc, Free, Getc, Putc
  If SliceEqualsStr(nPos, nLen, "Malloc", 6) Or SliceEqualsStr(nPos, nLen, "Fopen", 5) Or SliceEqualsStr(nPos, nLen, "Calloc", 6) Or SliceEqualsStr(nPos, nLen, "Realloc", 7) Then
    Dim bi As Integer = 0
    While bi < aCount Do
      Dim aNode As Integer = g_kids(aStart + bi)
      If g_nodes(aNode).op = 1 Then
        Printf("Semantic Type Error: Built-in function does not accept ByRef arguments\n")
        ta_errCount = ta_errCount + 1
      End If
      TaTypeOfExpr(g_nodes(aNode).left)
      bi = bi + 1
    End While
    Return TaMakePrim(TyPointer)
  End If

  If SliceEqualsStr(nPos, nLen, "Free", 4) Then
    Dim bi As Integer = 0
    While bi < aCount Do
      Dim aNode As Integer = g_kids(aStart + bi)
      If g_nodes(aNode).op = 1 Then
        Printf("Semantic Type Error: Free does not accept ByRef arguments\n")
        ta_errCount = ta_errCount + 1
      End If
      TaTypeOfExpr(g_nodes(aNode).left)
      bi = bi + 1
    End While
    Return TaMakePrim(TyNothing)
  End If

  If SliceEqualsStr(nPos, nLen, "Getc", 4) Or SliceEqualsStr(nPos, nLen, "Putc", 4) Then
    For bi = 0 To aCount - 1 Do
      Dim aNode As Integer = g_kids(aStart + bi)
      If g_nodes(aNode).op = 1 Then
        Printf("Semantic Type Error: Built-in function does not accept ByRef arguments\n")
        ta_errCount = ta_errCount + 1
      End If
      TaTypeOfExpr(g_nodes(aNode).left)
    End For
    Return TaMakePrim(TyInt)
  End If

  Dim uIdx = -1, vIdx = -1 As Integer
  Dim uMatch As Integer = TaFindUnqualUnionVariant(nPos, nLen, ByRef uIdx, ByRef vIdx)
  If uMatch = 1 Then
    Dim argIds(64) As Integer
    For ai = 0 To aCount - 1 Do
      Dim aNode As Integer = g_kids(aStart + ai)
      If g_nodes(aNode).op = 1 Then
        Printf(MSG_TA_UNION_BYREF)
        ta_errCount = ta_errCount + 1
      End If
      argIds(ai) = g_nodes(aNode).left
    End For
    Return TaCheckUnionConstruct(uIdx, nPos, nLen, argIds, aCount)
  End If
  If uMatch > 1 Then
    Printf("Semantic Type Error: Ambiguous union variant '")
    TaPrintSlice(nPos, nLen)
    Printf("' (qualify with UnionName.Variant)\n")
    ta_errCount = ta_errCount + 1
    Return TaMakePrim(TyInt)
  End If

  If TaHasPrivateFuncOther(nPos, nLen) Then
    Printf("Semantic Type Error: Cannot access Private function '")
    TaPrintSlice(nPos, nLen)
    Printf("' from included module\n")
    ta_errCount = ta_errCount + 1
    Return TaMakePrim(TyInt)
  End If

  Printf("Semantic Type Error: Undefined function or constructor: ")
  TaPrintSlice(nPos, nLen)
  Printf("\n")
  ta_errCount = ta_errCount + 1
  Return TaMakePrim(TyInt)
End Function

Private Function TaCheckIndirectCall(fpTy As TypeInfo, aStart, aCount, argsAreNdArg As Integer) As TypeInfo
  If (fpTy.arrLen <> 0) Or (fpTy.kind <> TyFuncPtr) Or (fpTy.typeIdx < 0) Or (fpTy.typeIdx >= ta_fpSigCount) Then
    Printf(MSG_TA_FP_CALL_NON_FP)
    ta_errCount = ta_errCount + 1
    For ai = 0 To aCount - 1 Do
      Dim kid As Integer = g_kids(aStart + ai)
      If argsAreNdArg = 1 Then
        TaTypeOfExpr(g_nodes(kid).left)
      Else
        TaTypeOfExpr(kid)
      End If
    End For
    Return TaMakePrim(TyInt)
  End If
  Dim fs As TaFpSig = ta_fpSigs(fpTy.typeIdx)
  If aCount <> fs.paramCount Then
    Printf("Semantic Type Error: Function pointer call expects %d argument(s), got %d\n", fs.paramCount, aCount)
    ta_errCount = ta_errCount + 1
  End If
  For ai = 0 To aCount - 1 Do
    Dim kid As Integer = g_kids(aStart + ai)
    Dim argMode As Integer = 0
    Dim argExpr As Integer = kid
    If argsAreNdArg = 1 Then
      argMode = g_nodes(kid).op
      argExpr = g_nodes(kid).left
    End If
    Dim actualTy As TypeInfo = TaTypeOfExpr(argExpr)
    If ai < fs.paramCount Then
      Dim pInfo As TaParamInfo = ta_fpParams(fs.paramStart + ai)
      If pInfo.isByRef = 1 Then
        If argMode <> 1 Then
          Printf("Semantic Type Error: Argument %d of function pointer call expects ByRef at call site\n", ai + 1)
          ta_errCount = ta_errCount + 1
        Else
          If Not TaIsLValue(argExpr) Then
            Printf("Semantic Type Error: ByRef argument must be a variable, struct field, or array element\n")
            ta_errCount = ta_errCount + 1
          End If
        End If
      Else
        If argMode = 1 Then
          Printf("Semantic Type Error: Argument %d of function pointer call is ByVal and cannot be passed ByRef\n", ai + 1)
          ta_errCount = ta_errCount + 1
        End If
      End If
      If Not TaIsAssignable(pInfo.ty, actualTy, argExpr) Then
        Printf("Semantic Type Error: Argument %d of function pointer call type mismatch: expected ", ai + 1)
        TaPrintType(pInfo.ty)
        Printf(", got ")
        TaPrintType(actualTy)
        Printf("\n")
        ta_errCount = ta_errCount + 1
      End If
    End If
  End For
  Return fs.retTy
End Function

Private Function TaCheckIndirectCallOne(fpTy As TypeInfo, argExpr As Integer) As TypeInfo
  Dim fs As TaFpSig = ta_fpSigs(fpTy.typeIdx)
  Dim actualTy As TypeInfo = TaTypeOfExpr(argExpr)
  If fs.paramCount <> 1 Then
    Printf("Semantic Type Error: Function pointer call expects %d argument(s), got 1\n", fs.paramCount)
    ta_errCount = ta_errCount + 1
    Return fs.retTy
  End If
  Dim pInfo As TaParamInfo = ta_fpParams(fs.paramStart)
  If pInfo.isByRef = 1 Then
    Printf("Semantic Type Error: Argument 1 of function pointer call expects ByRef at call site\n")
    ta_errCount = ta_errCount + 1
  End If
  If Not TaIsAssignable(pInfo.ty, actualTy, argExpr) Then
    Printf("Semantic Type Error: Argument 1 of function pointer call type mismatch: expected ")
    TaPrintType(pInfo.ty)
    Printf(", got ")
    TaPrintType(actualTy)
    Printf("\n")
    ta_errCount = ta_errCount + 1
  End If
  Return fs.retTy
End Function

Private Function TaCheckUnionConstruct(uIdx, vPos, vLen As Integer, argIds() As Integer, argCnt As Integer) As TypeInfo
  Dim uTy As TypeInfo With ( kind = TyUnion, typeIdx = uIdx, arrLen = 0, namePos = ta_unions(uIdx).namePos, nameLen = ta_unions(uIdx).nameLen )
  Dim vIdx As Integer = TaFindUnionVariant(uIdx, vPos, vLen)
  If vIdx < 0 Then
    Printf("Semantic Type Error: Union '")
    TaPrintSlice(ta_unions(uIdx).namePos, ta_unions(uIdx).nameLen)
    Printf("' has no variant named '")
    TaPrintSlice(vPos, vLen)
    Printf("'\n")
    ta_errCount = ta_errCount + 1
    Return uTy
  End If
  Dim vInfo As TaUnionVar = ta_unionVars(ta_unions(uIdx).varStart + vIdx)
  If argCnt <> vInfo.fieldCount Then
    Printf("Semantic Type Error: Union variant '")
    TaPrintSlice(ta_unions(uIdx).namePos, ta_unions(uIdx).nameLen)
    Printf(".")
    TaPrintSlice(vPos, vLen)
    Printf("' expects %d argument(s), got %d\n", vInfo.fieldCount, argCnt)
    ta_errCount = ta_errCount + 1
  End If
  Dim lim As Integer = argCnt
  If vInfo.fieldCount < lim Then
    lim = vInfo.fieldCount
  End If
  For i = 0 To lim - 1 Do
    Dim aTy As TypeInfo = TaTypeOfExpr(argIds(i))
    Dim fTy As TypeInfo = ta_fields(vInfo.fieldStart + i).ty
    If Not TaIsAssignable(fTy, aTy, argIds(i)) Then
      Printf("Semantic Type Error: Type mismatch for argument %d of union variant '", i + 1)
      TaPrintSlice(vPos, vLen)
      Printf("': expected ")
      TaPrintType(fTy)
      Printf(", got ")
      TaPrintType(aTy)
      Printf("\n")
      ta_errCount = ta_errCount + 1
    End If
  End For
  Return uTy
End Function

Private Function TaTypeOfExpr(id As Integer) As TypeInfo
  If id < 0 Then
    Return TaMakePrim(TyInt)
  End If
  Dim k As Integer = g_nodes(id).kind

  If k = NdIntLit Then
    Dim v As Long = g_nodes(id).longVal
    If (v > 2147483647) Or (v < 0) Then
      Return TaMakePrim(TyLong)
    End If
    Return TaMakePrim(TyInt)
  End If
  If k = NdFloatLit Then
    Return TaMakePrim(TyDouble)
  End If
  If k = NdBoolLit Then
    Return TaMakePrim(TyBoolean)
  End If
  If k = NdStrLit Then
    Return TaMakePrim(TyPointer)
  End If
  If k = NdStrInterp Then
    For ii = 0 To g_nodes(id).childCount - 1 Do
      Dim ch As Integer = g_kids(g_nodes(id).firstChild + ii)
      If g_nodes(ch).kind <> NdStrLit Then
        Dim ct As TypeInfo = TaTypeOfExpr(ch)
        Dim okPart As Boolean = TaIsIntType(ct) Or TaIsFloatType(ct) Or ((ct.arrLen = 0) And (ct.kind = TyBoolean)) Or TaIsStrLike(ct)
        If Not okPart Then
          Printf(MSG_TA_INTERP_TYPE)
          ta_errCount = ta_errCount + 1
        End If
      End If
    End For
    Return TaMakePrim(TyPointer)
  End If

  If k = NdWithTarget Then
    If ta_withDepth > 0 Then
      Return ta_withTys(ta_withDepth - 1)
    End If
    Printf("Semantic Type Error: Leading '.' member access used outside With block\n")
    ta_errCount = ta_errCount + 1
    Return TaMakePrim(TyInt)
  End If

  If k = NdVar Then
    Dim nPos As Integer = g_nodes(id).intVal
    Dim nLen As Integer = g_nodes(id).lenVal
    Dim vTy As TypeInfo
    If TaLookupVar(nPos, nLen, ByRef vTy) Then
      Return vTy
    End If
    Dim eIdx As Integer = TaFindEnum(nPos, nLen)
    If eIdx >= 0 Then
      Dim et As TypeInfo With ( kind = TyEnum, typeIdx = eIdx, arrLen = 0, namePos = nPos, nameLen = nLen )
      Return et
    End If
    Dim uEnumIdx As Integer = -1
    Dim uCnt As Integer = TaFindUnqualEnumMem(nPos, nLen, ByRef uEnumIdx)
    If uCnt = 1 Then
      Dim et As TypeInfo With ( kind = TyEnum, typeIdx = uEnumIdx, arrLen = 0, namePos = ta_enums(uEnumIdx).namePos, nameLen = ta_enums(uEnumIdx).nameLen )
      Return et
    End If
    If uCnt > 1 Then
      Printf("Semantic Type Error: Ambiguous enum member '")
      TaPrintSlice(nPos, nLen)
      Printf("' (qualify with EnumName.Member)\n")
      ta_errCount = ta_errCount + 1
      Return TaMakePrim(TyInt)
    End If
    Dim uuIdx = -1, uvIdx = -1 As Integer
    Dim uvCnt As Integer = TaFindUnqualUnionVariant(nPos, nLen, ByRef uuIdx, ByRef uvIdx)
    If uvCnt = 1 Then
      Dim emptyArgs(1) As Integer
      Return TaCheckUnionConstruct(uuIdx, nPos, nLen, emptyArgs, 0)
    End If
    If uvCnt > 1 Then
      Printf("Semantic Type Error: Ambiguous union variant '")
      TaPrintSlice(nPos, nLen)
      Printf("' (qualify with UnionName.Variant)\n")
      ta_errCount = ta_errCount + 1
      Return TaMakePrim(TyInt)
    End If
    If TaHasPrivateGlobalOther(nPos, nLen) Or TaHasPrivateFuncOther(nPos, nLen) Then
      Printf("Semantic Type Error: Cannot access Private symbol '")
      TaPrintSlice(nPos, nLen)
      Printf("' from included module\n")
      ta_errCount = ta_errCount + 1
      Return TaMakePrim(TyInt)
    End If
    Printf("Semantic Type Error: Undefined variable or constant reference: ")
    TaPrintSlice(nPos, nLen)
    Printf("\n")
    ta_errCount = ta_errCount + 1
    Return TaMakePrim(TyInt)
  End If

  If k = NdNot Then
    Dim t As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
    If (t.arrLen = 0) And ((t.kind = TyStruct) Or (t.kind = TyUnion)) Then
      Dim uoi As Integer = TaFindUnaryOp(TkNot, t)
      If uoi >= 0 Then
        Return ta_ops(uoi).retTy
      End If
    End If
    If ((t.arrLen = 0) And (t.kind = TyBoolean)) Or TaIsIntType(t) Then
      Return t
    End If
    Printf("Semantic Type Error: Invalid unary operation 'Not'\n")
    ta_errCount = ta_errCount + 1
    Return t
  End If

  If k = NdNeg Then
    Dim t As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
    If (t.arrLen = 0) And ((t.kind = TyStruct) Or (t.kind = TyUnion)) Then
      Dim uoi As Integer = TaFindUnaryOp(TkMinus, t)
      If uoi >= 0 Then
        Return ta_ops(uoi).retTy
      End If
    End If
    If TaIsIntType(t) Or TaIsFloatType(t) Then
      Return t
    End If
    Printf("Semantic Type Error: Invalid unary operation 'Neg'\n")
    ta_errCount = ta_errCount + 1
    Return t
  End If

  If k = NdBinOp Then
    Dim e1 As Integer = g_nodes(id).left
    Dim e2 As Integer = g_nodes(id).right
    Dim op As Integer = g_nodes(id).op
    Dim t1 As TypeInfo = TaTypeOfExpr(e1)
    Dim t2 As TypeInfo = TaTypeOfExpr(e2)
    Dim hasStructOp As Boolean = ((t1.arrLen = 0) And ((t1.kind = TyStruct) Or (t1.kind = TyUnion))) Or ((t2.arrLen = 0) And ((t2.kind = TyStruct) Or (t2.kind = TyUnion)))
    If hasStructOp Then
      Dim boi As Integer = TaFindBinOp(op, t1, e1, t2, e2)
      If boi >= 0 Then
        Return ta_ops(boi).retTy
      End If
      Printf(MSG_TA_OP_NO_OVERLOAD)
      ta_errCount = ta_errCount + 1
      Return t1
    End If
    If op = TkAmp Then
      If TaIsStrLike(t1) And TaIsStrLike(t2) Then
        Return TaMakePrim(TyPointer)
      End If
      Printf(MSG_TA_STR_CONCAT_ERR)
      ta_errCount = ta_errCount + 1
      Return TaMakePrim(TyPointer)
    End If
    If (op = TkPlus) And TaIsStrLike(t1) And TaIsStrLike(t2) Then
      Return TaMakePrim(TyPointer)
    End If
    Dim unified As TypeInfo = t1
    Dim okUnify As Boolean = False
    If ((op = TkEq) Or (op = TkNeq)) And ((((t1.arrLen = 0) And (t1.kind = TyFuncPtr)) And (g_nodes(e2).kind = NdIntLit) And (g_nodes(e2).longVal = 0)) Or (((t2.arrLen = 0) And (t2.kind = TyFuncPtr)) And (g_nodes(e1).kind = NdIntLit) And (g_nodes(e1).longVal = 0))) Then
      Return TaMakePrim(TyBoolean)
    End If
    If TaTypesEqual(t1, t2) Then
      unified = t1
      okUnify = True
    Else
      If TaIsIntType(t1) And TaIsIntLitNode(e2) Then
        unified = t1
        okUnify = True
      Else
        If TaIsIntType(t2) And TaIsIntLitNode(e1) Then
          unified = t2
          okUnify = True
        Else
          If TaIsFloatType(t1) And TaIsFloatLitNode(e2) Then
            unified = t1
            okUnify = True
          Else
            If TaIsFloatType(t2) And TaIsFloatLitNode(e1) Then
              unified = t2
              okUnify = True
            End If
          End If
        End If
      End If
    End If
    If Not okUnify Then
      Printf(MSG_TA_BINOP_MISMATCH)
      TaPrintType(t1)
      Printf(" vs ")
      TaPrintType(t2)
      Printf("\n")
      ta_errCount = ta_errCount + 1
      Return t1
    End If
    Dim isCmp As Boolean = (op = TkEq) Or (op = TkNeq) Or (op = TkLt) Or (op = TkGt) Or (op = TkLe) Or (op = TkGe)
    If (unified.arrLen = 0) And (unified.kind = TyFuncPtr) Then
      If (op = TkEq) Or (op = TkNeq) Then
        Return TaMakePrim(TyBoolean)
      End If
      Printf(MSG_TA_BINOP_MISMATCH)
      TaPrintType(t1)
      Printf(" vs ")
      TaPrintType(t2)
      Printf("\n")
      ta_errCount = ta_errCount + 1
      Return TaMakePrim(TyBoolean)
    End If
    If TaIsFloatType(unified) Then
      If (op = TkAnd) Or (op = TkOr) Or (op = TkXor) Or (op = TkShl) Or (op = TkShr) Then
        Printf(MSG_TA_FLOAT_BITWISE)
        ta_errCount = ta_errCount + 1
        Return unified
      End If
      If isCmp Then
        Return TaMakePrim(TyBoolean)
      End If
      Return unified
    End If
    If isCmp Then
      Return TaMakePrim(TyBoolean)
    End If
    Return unified
  End If

  If k = NdFieldAccess Then
    Dim baseId As Integer = g_nodes(id).left
    Dim fPos As Integer = g_nodes(id).intVal
    Dim fLen As Integer = g_nodes(id).lenVal
    If (baseId >= 0) And (g_nodes(baseId).kind = NdVar) Then
      Dim eIdx As Integer = TaFindEnum(g_nodes(baseId).intVal, g_nodes(baseId).lenVal)
      If eIdx >= 0 Then
        If TaHasEnumMem(eIdx, fPos, fLen) Then
          Return TaMakePrim(TyInt)
        End If
        Printf("Semantic Type Error: Enum '")
        TaPrintSlice(g_nodes(baseId).intVal, g_nodes(baseId).lenVal)
        Printf("' has no member variant: ")
        TaPrintSlice(fPos, fLen)
        Printf("\n")
        ta_errCount = ta_errCount + 1
        Return TaMakePrim(TyInt)
      End If
      Dim dummyVarTy As TypeInfo
      If Not TaLookupVar(g_nodes(baseId).intVal, g_nodes(baseId).lenVal, ByRef dummyVarTy) Then
        Dim uIdx As Integer = TaFindUnion(g_nodes(baseId).intVal, g_nodes(baseId).lenVal)
        If uIdx >= 0 Then
          Dim emptyArgs(1) As Integer
          Return TaCheckUnionConstruct(uIdx, fPos, fLen, emptyArgs, 0)
        End If
      End If
    End If
    Dim baseTy As TypeInfo = TaTypeOfExpr(baseId)
    If (baseTy.arrLen = 0) And (baseTy.kind = TyStruct) And (baseTy.typeIdx >= 0) Then
      Dim fTy As TypeInfo
      Dim fIdx As Integer = TaFindStructField(baseTy.typeIdx, fPos, fLen, ByRef fTy)
      If fIdx >= 0 Then
        Return fTy
      End If
      Printf("Semantic Type Error: Structure '")
      TaPrintSlice(baseTy.namePos, baseTy.nameLen)
      Printf("' has no field named '")
      TaPrintSlice(fPos, fLen)
      Printf("'\n")
      ta_errCount = ta_errCount + 1
      Return TaMakePrim(TyInt)
    End If
    Printf("Semantic Type Error: Field access requires a structure value before .")
    TaPrintSlice(fPos, fLen)
    Printf("\n")
    ta_errCount = ta_errCount + 1
    Return TaMakePrim(TyInt)
  End If

  If k = NdIndexAccess Then
    Dim faNode As Integer = g_nodes(id).left
    If (faNode >= 0) And (g_nodes(faNode).kind = NdFieldAccess) Then
      Dim baseVar As Integer = g_nodes(faNode).left
      If (baseVar >= 0) And (g_nodes(baseVar).kind = NdVar) Then
        Dim dummyVarTy As TypeInfo
        If Not TaLookupVar(g_nodes(baseVar).intVal, g_nodes(baseVar).lenVal, ByRef dummyVarTy) Then
          Dim uIdx As Integer = TaFindUnion(g_nodes(baseVar).intVal, g_nodes(baseVar).lenVal)
          If uIdx >= 0 Then
            Dim oneArg(1) As Integer
            oneArg(0) = g_nodes(id).right
            Return TaCheckUnionConstruct(uIdx, g_nodes(faNode).intVal, g_nodes(faNode).lenVal, oneArg, 1)
          End If
        End If
      End If
    End If
    Dim baseTy As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
    If (baseTy.arrLen = 0) And (baseTy.kind = TyFuncPtr) And (baseTy.typeIdx >= 0) Then
      Return TaCheckIndirectCallOne(baseTy, g_nodes(id).right)
    End If
    Return TaCheckIndex(baseTy, g_nodes(id).right)
  End If

  If k = NdIndirectCall Then
    Dim calleeTy As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
    Return TaCheckIndirectCall(calleeTy, g_nodes(id).firstChild, g_nodes(id).childCount, 1)
  End If

  If k = NdUnionInit Then
    Dim baseNode As Integer = g_nodes(id).left
    If (baseNode >= 0) And (g_nodes(baseNode).kind = NdVar) Then
      Dim dummyVarTy As TypeInfo
      If Not TaLookupVar(g_nodes(baseNode).intVal, g_nodes(baseNode).lenVal, ByRef dummyVarTy) Then
        Dim uIdx As Integer = TaFindUnion(g_nodes(baseNode).intVal, g_nodes(baseNode).lenVal)
        If uIdx >= 0 Then
          Dim argIds(64) As Integer
          Dim aCnt As Integer = g_nodes(id).childCount
          For i = 0 To aCnt - 1 Do
            argIds(i) = g_kids(g_nodes(id).firstChild + i)
          End For
          Return TaCheckUnionConstruct(uIdx, g_nodes(id).intVal, g_nodes(id).lenVal, argIds, aCnt)
        End If
        If TaHasPrivateUnionOther(g_nodes(baseNode).intVal, g_nodes(baseNode).lenVal) Then
          Printf(MSG_TA_PRIVATE_TYPE)
          TaPrintSlice(g_nodes(baseNode).intVal, g_nodes(baseNode).lenVal)
          Printf(MSG_TA_FROM_MOD_SUFFIX)
          ta_errCount = ta_errCount + 1
          Return TaMakePrim(TyInt)
        End If
        Printf("Semantic Type Error: Unknown Union type '")
        TaPrintSlice(g_nodes(baseNode).intVal, g_nodes(baseNode).lenVal)
        Printf("'\n")
        ta_errCount = ta_errCount + 1
        Return TaMakePrim(TyInt)
      End If
    End If
    ' Indirect call on a struct field (e.g. obj.cb() or obj.cb(a, b) or .cb(a, b))
    Dim baseTy As TypeInfo = TaTypeOfExpr(baseNode)
    If (baseTy.arrLen = 0) And (baseTy.kind = TyStruct) And (baseTy.typeIdx >= 0) Then
      Dim fTy As TypeInfo
      If TaFindStructField(baseTy.typeIdx, g_nodes(id).intVal, g_nodes(id).lenVal, ByRef fTy) >= 0 Then
        Return TaCheckIndirectCall(fTy, g_nodes(id).firstChild, g_nodes(id).childCount, 0)
      End If
      Printf("Semantic Type Error: Structure '")
      TaPrintSlice(baseTy.namePos, baseTy.nameLen)
      Printf("' has no field named '")
      TaPrintSlice(g_nodes(id).intVal, g_nodes(id).lenVal)
      Printf("'\n")
      ta_errCount = ta_errCount + 1
      Return TaMakePrim(TyInt)
    End If
    Printf(MSG_TA_FP_CALL_NON_FP)
    ta_errCount = ta_errCount + 1
    Return TaMakePrim(TyInt)
  End If

  If k = NdArrayLit Then
    Dim cnt As Integer = g_nodes(id).childCount
    If cnt = 0 Then
      Dim emptyArr As TypeInfo With ( kind = TyInt, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
      Return emptyArr
    End If
    Dim firstTy As TypeInfo = TaTypeOfExpr(g_kids(g_nodes(id).firstChild))
    Dim i As Integer = 1
    While i < cnt Do
      TaTypeOfExpr(g_kids(g_nodes(id).firstChild + i))
      i = i + 1
    End While
    Dim arrTy As TypeInfo With ( kind = firstTy.kind, typeIdx = firstTy.typeIdx, arrLen = cnt, namePos = firstTy.namePos, nameLen = firstTy.nameLen, innerKind = firstTy.innerKind )
    Return arrTy
  End If

  If k = NdStructInit Then
    Dim sPos As Integer = g_nodes(id).intVal
    Dim sLen As Integer = g_nodes(id).lenVal
    Dim sIdx As Integer = TaFindStruct(sPos, sLen)
    If sIdx < 0 Then
      Printf("Semantic Type Error: Unknown structure in With initializer: ")
      TaPrintSlice(sPos, sLen)
      Printf("\n")
      ta_errCount = ta_errCount + 1
      Return TaMakePrim(TyInt)
    End If
    Dim st As Integer = g_nodes(id).firstChild
    Dim cnt As Integer = g_nodes(id).childCount
    For i = 0 To cnt - 1 Do
      Dim fi As Integer = g_kids(st + i)
      Dim fnPos As Integer = g_nodes(fi).intVal
      Dim fnLen As Integer = g_nodes(fi).lenVal
      ' Check duplicate field in With initializer
      For prev = 0 To i - 1 Do
        Dim pfi As Integer = g_kids(st + prev)
        If SlicesEqual(g_nodes(pfi).intVal, g_nodes(pfi).lenVal, fnPos, fnLen) Then
          Printf("Semantic Type Error: Duplicate field '")
          TaPrintSlice(fnPos, fnLen)
          Printf("' in '")
          TaPrintSlice(sPos, sLen)
          Printf("' With initializer\n")
          ta_errCount = ta_errCount + 1
        End If
      End For
      Dim fTy As TypeInfo
      Dim fIdx As Integer = TaFindStructField(sIdx, fnPos, fnLen, ByRef fTy)
      If fIdx < 0 Then
        Printf("Semantic Type Error: Structure '")
        TaPrintSlice(sPos, sLen)
        Printf("' has no field named '")
        TaPrintSlice(fnPos, fnLen)
        Printf("'\n")
        ta_errCount = ta_errCount + 1
      Else
        Dim valTy As TypeInfo = TaTypeOfExpr(g_nodes(fi).left)
        If Not TaIsAssignable(fTy, valTy, g_nodes(fi).left) Then
          Printf("Semantic Type Error: Type mismatch for field '")
          TaPrintSlice(fnPos, fnLen)
          Printf("' in '")
          TaPrintSlice(sPos, sLen)
          Printf("' With initializer\n")
          ta_errCount = ta_errCount + 1
        End If
      End If
    End For
    Dim res As TypeInfo With ( kind = TyStruct, typeIdx = sIdx, arrLen = 0, namePos = sPos, nameLen = sLen )
    Return res
  End If

  If k = NdSizeOf Then
    Dim ty As TypeInfo = TaResolveType(g_nodes(id).tyKind, 0, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    TaValidateType(ty)
    Return TaMakePrim(TyInt)
  End If

  If k = NdCast Then
    Dim srcTy As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
    Dim dstKind As Integer = g_nodes(id).tyKind
    Dim dstTy As TypeInfo = TaMakePrim(dstKind)
    Dim srcNum As Boolean = TaIsIntType(srcTy) Or TaIsFloatType(srcTy)
    Dim srcBool As Boolean = (srcTy.arrLen = 0) And (srcTy.kind = TyBoolean)
    Dim srcPtr As Boolean = ((srcTy.arrLen = 0) And ((srcTy.kind = TyPointer) Or (srcTy.kind = TyFuncPtr))) Or (srcTy.arrLen <> 0)
    Dim dstNum As Boolean = TaIsIntType(dstTy) Or TaIsFloatType(dstTy)
    Dim ok As Boolean = False
    If dstNum And (srcNum Or srcBool) Then
      ok = True
    End If
    If TaIsIntType(dstTy) And srcPtr Then
      ok = True
    End If
    If (dstKind = TyBoolean) And (srcNum Or srcBool Or srcPtr) Then
      ok = True
    End If
    If (dstKind = TyPointer) And (srcPtr Or TaIsIntType(srcTy)) Then
      ok = True
    End If
    If Not ok Then
      Dim tname As String = "primitive"
      Match dstKind With
        Case TyByte Then tname = "Byte"
        Case TyShort Then tname = "Short"
        Case TyInt Then tname = "Integer"
        Case TyLong Then tname = "Long"
        Case TySingle Then tname = "Single"
        Case TyDouble Then tname = "Double"
        Case TyBoolean Then tname = "Boolean"
        Case TyPointer Then tname = "Pointer"
      End Match
      Printf("Semantic Type Error: Cannot convert expression to %s\n", tname)
      ta_errCount = ta_errCount + 1
    End If
    Return dstTy
  End If

  If k = NdHeapAlloc Then
    Dim elemTy As TypeInfo = TaResolveType(g_nodes(id).tyKind, 0, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    TaValidateType(elemTy)
    If elemTy.kind = TyNothing Then
      Printf("Semantic Type Error: HeapAlloc requires a non-empty element type\n")
      ta_errCount = ta_errCount + 1
    End If
    Dim cntTy As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
    If Not TaIsIntType(cntTy) Then
      Printf("Semantic Type Error: HeapAlloc count must be an integer type\n")
      ta_errCount = ta_errCount + 1
    End If
    Dim res As TypeInfo With ( kind = elemTy.kind, typeIdx = elemTy.typeIdx, arrLen = -1, namePos = elemTy.namePos, nameLen = elemTy.nameLen )
    Return res
  End If

  If k = NdHeapRealloc Then
    Dim elemTy As TypeInfo = TaResolveType(g_nodes(id).tyKind, 0, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    TaValidateType(elemTy)
    Dim pt As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
    If (pt.arrLen = 0) And (pt.kind <> TyPointer) Then
      Printf("Semantic Type Error: HeapRealloc expects a Pointer or heap array\n")
      ta_errCount = ta_errCount + 1
    End If
    Dim cntTy As TypeInfo = TaTypeOfExpr(g_nodes(id).right)
    If Not TaIsIntType(cntTy) Then
      Printf("Semantic Type Error: HeapRealloc count must be an integer type\n")
      ta_errCount = ta_errCount + 1
    End If
    Dim res As TypeInfo With ( kind = elemTy.kind, typeIdx = elemTy.typeIdx, arrLen = -1, namePos = elemTy.namePos, nameLen = elemTy.nameLen )
    Return res
  End If

  If k = NdVolatileLoad Then
    Dim addrTy As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
    Dim addrOk As Boolean = ((addrTy.arrLen = 0) And (addrTy.kind = TyPointer)) Or (addrTy.arrLen <> 0) Or TaIsIntType(addrTy)
    If Not addrOk Then
      Printf(MSG_TA_VOLATILE_ADDR)
      ta_errCount = ta_errCount + 1
    End If
    Dim valTy As TypeInfo = TaResolveType(g_nodes(id).tyKind, 0, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    TaValidateType(valTy)
    If valTy.kind = TyNothing Then
      Printf(MSG_TA_VOLATILE_TYPE)
      ta_errCount = ta_errCount + 1
    End If
    Return valTy
  End If

  If k = NdVolatileStore Then
    Dim addrTy As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
    Dim addrOk As Boolean = ((addrTy.arrLen = 0) And (addrTy.kind = TyPointer)) Or (addrTy.arrLen <> 0) Or TaIsIntType(addrTy)
    If Not addrOk Then
      Printf(MSG_TA_VOLATILE_ADDR)
      ta_errCount = ta_errCount + 1
    End If
    Dim targetTy As TypeInfo = TaResolveTypeEx(g_nodes(id).tyKind, g_nodes(id).tyInnerKind, 0, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    TaValidateType(targetTy)
    If targetTy.kind = TyNothing Then
      Printf(MSG_TA_VOLATILE_TYPE)
      ta_errCount = ta_errCount + 1
    End If
    Dim valTy As TypeInfo = TaTypeOfExpr(g_nodes(id).right)
    If Not TaIsAssignable(targetTy, valTy, g_nodes(id).right) Then
      Printf(MSG_TA_VOLATILE_STORE_MISMATCH)
      ta_errCount = ta_errCount + 1
    End If
    Return TaMakePrim(TyNothing)
  End If

  If k = NdAddressOf Then
    Dim tgt As Integer = g_nodes(id).left
    If tgt >= 0 Then
      Dim tk As Integer = g_nodes(tgt).kind
      If tk = NdVar Then
        Dim vp = g_nodes(tgt).intVal, vl = g_nodes(tgt).lenVal As Integer
        Dim dummyTy As TypeInfo
        If TaLookupVar(vp, vl, ByRef dummyTy) Then
          Return TaMakePrim(TyPointer)
        End If
        Dim fIdx As Integer = TaFindFunc(vp, vl)
        If fIdx >= 0 Then
          If ta_funcs(fIdx).isAsync = 1 Then
            Printf(MSG_TA_FP_ASYNC)
            ta_errCount = ta_errCount + 1
            Return TaMakePrim(TyPointer)
          End If
          If ta_funcs(fIdx).isVariadic = 1 Then
            Return TaMakePrim(TyPointer)
          End If
          Dim sigIdx As Integer = TaRegisterFpSigFromFunc(fIdx)
          Dim fpRes As TypeInfo With ( kind = TyFuncPtr, typeIdx = sigIdx, arrLen = 0, namePos = 0, nameLen = 0, innerKind = TyNothing )
          Return fpRes
        End If
        If TaHasPrivateFuncOther(vp, vl) Then
          Printf("Semantic Type Error: Cannot access Private function '")
          TaPrintSlice(vp, vl)
          Printf("' from included module\n")
          ta_errCount = ta_errCount + 1
          Return TaMakePrim(TyPointer)
        End If
        Dim dummyEnumIdx = -1, dummyU = -1, dummyV = -1 As Integer
        If (TaFindEnum(vp, vl) >= 0) Or (TaFindUnqualEnumMem(vp, vl, ByRef dummyEnumIdx) > 0) Or (TaFindUnion(vp, vl) >= 0) Or (TaFindUnqualUnionVariant(vp, vl, ByRef dummyU, ByRef dummyV) > 0) Then
          Printf(MSG_TA_ADDRESSOF_TARGET)
          ta_errCount = ta_errCount + 1
          Return TaMakePrim(TyPointer)
        End If
        TaTypeOfExpr(tgt)
        Return TaMakePrim(TyPointer)
      End If
      If tk = NdFieldAccess Then
        Dim baseId As Integer = g_nodes(tgt).left
        If (baseId >= 0) And (g_nodes(baseId).kind = NdVar) Then
          If (TaFindEnum(g_nodes(baseId).intVal, g_nodes(baseId).lenVal) >= 0) Or (TaFindUnion(g_nodes(baseId).intVal, g_nodes(baseId).lenVal) >= 0) Then
            Printf(MSG_TA_ADDRESSOF_TARGET)
            ta_errCount = ta_errCount + 1
            Return TaMakePrim(TyPointer)
          End If
        End If
        TaTypeOfExpr(tgt)
        Return TaMakePrim(TyPointer)
      End If
      If TaIsLValue(tgt) Then
        TaTypeOfExpr(tgt)
        Return TaMakePrim(TyPointer)
      End If
    End If
    Printf(MSG_TA_ADDRESSOF_TARGET)
    ta_errCount = ta_errCount + 1
    Return TaMakePrim(TyPointer)
  End If

  If k = NdPtrRead Then
    Dim addrTy As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
    Dim addrOk As Boolean = ((addrTy.arrLen = 0) And (addrTy.kind = TyPointer)) Or (addrTy.arrLen <> 0) Or TaIsIntType(addrTy)
    If Not addrOk Then
      Printf(MSG_TA_PTR_ADDR)
      ta_errCount = ta_errCount + 1
    End If
    Dim valTy As TypeInfo = TaResolveTypeEx(g_nodes(id).tyKind, g_nodes(id).tyInnerKind, 0, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    TaValidateType(valTy)
    If valTy.kind = TyNothing Then
      Printf(MSG_TA_PTR_TYPE)
      ta_errCount = ta_errCount + 1
    End If
    Return valTy
  End If

  If k = NdPtrWrite Then
    Dim addrTy As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
    Dim addrOk As Boolean = ((addrTy.arrLen = 0) And (addrTy.kind = TyPointer)) Or (addrTy.arrLen <> 0) Or TaIsIntType(addrTy)
    If Not addrOk Then
      Printf(MSG_TA_PTR_ADDR)
      ta_errCount = ta_errCount + 1
    End If
    Dim targetTy As TypeInfo = TaResolveTypeEx(g_nodes(id).tyKind, g_nodes(id).tyInnerKind, 0, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    TaValidateType(targetTy)
    If targetTy.kind = TyNothing Then
      Printf(MSG_TA_PTR_TYPE)
      ta_errCount = ta_errCount + 1
    End If
    Dim valTy As TypeInfo = TaTypeOfExpr(g_nodes(id).right)
    If Not TaIsAssignable(targetTy, valTy, g_nodes(id).right) Then
      Printf(MSG_TA_PTR_WRITE_MISMATCH)
      ta_errCount = ta_errCount + 1
    End If
    Return TaMakePrim(TyNothing)
  End If

  If k = NdPtrAdd Then
    Dim addrTy As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
    Dim addrOk As Boolean = ((addrTy.arrLen = 0) And (addrTy.kind = TyPointer)) Or (addrTy.arrLen <> 0) Or TaIsIntType(addrTy)
    If Not addrOk Then
      Printf(MSG_TA_PTR_ADDR)
      ta_errCount = ta_errCount + 1
    End If
    Dim offTy As TypeInfo = TaTypeOfExpr(g_nodes(id).right)
    If Not TaIsIntType(offTy) Then
      Printf(MSG_TA_PTR_OFFSET_INT)
      ta_errCount = ta_errCount + 1
    End If
    Return TaMakePrim(TyPointer)
  End If

  If k = NdAwait Then
    Dim t As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
    If (t.arrLen <> 0) Or (t.kind <> TyTask) Then
      Printf(MSG_TA_AWAIT_NON_TASK)
      ta_errCount = ta_errCount + 1
      Return TaMakePrim(TyInt)
    End If
    Dim unwrapped As TypeInfo With ( kind = t.innerKind, typeIdx = t.typeIdx, arrLen = 0, namePos = t.namePos, nameLen = t.nameLen, innerKind = 0 )
    Return unwrapped
  End If

  If k = NdArenaAlloc Then
    If g_nodes(id).op <> 1 Then
      Printf("Semantic Type Error: ArenaAlloc expects ByRef arena as argument 1\n")
      ta_errCount = ta_errCount + 1
    Else
      If Not TaIsLValue(g_nodes(id).left) Then
        Printf("Semantic Type Error: ByRef argument must be a variable, struct field, or array element\n")
        ta_errCount = ta_errCount + 1
      Else
        Dim adt As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
        If (adt.arrLen <> 0) Or (adt.kind <> TyArena) Then
          Printf("Semantic Type Error: ArenaAlloc argument 1 must be an Arena variable\n")
          ta_errCount = ta_errCount + 1
        End If
      End If
    End If
    Dim elemTy As TypeInfo = TaResolveType(g_nodes(id).tyKind, 0, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    TaValidateType(elemTy)
    Dim cntTy As TypeInfo = TaTypeOfExpr(g_nodes(id).right)
    If Not TaIsIntType(cntTy) Then
      Printf("Semantic Type Error: ArenaAlloc count must be an integer type\n")
      ta_errCount = ta_errCount + 1
    End If
    Dim res As TypeInfo With ( kind = elemTy.kind, typeIdx = elemTy.typeIdx, arrLen = -1, namePos = elemTy.namePos, nameLen = elemTy.nameLen )
    Return res
  End If

  If k = NdCall Then
    Return TaTypeOfCall(id)
  End If

  Return TaMakePrim(TyInt)
End Function

Private Sub TaResetScopes()
  ta_curScopeId = 0
  ta_nextScopeId = 1
  ta_curDeferTotal = 0
  ta_scopeParent(0) = -1
  ta_scopeDeferCnt(0) = 0
End Sub

Private Function TaPushScope() As Integer
  Dim prev As Integer = ta_curScopeId
  If ta_deferDepth > 0 Then
    Return prev
  End If
  Dim nid As Integer = ta_nextScopeId
  If nid < 4096 Then
    ta_nextScopeId = nid + 1
    ta_scopeParent(nid) = prev
    ta_scopeDeferCnt(nid) = 0
    ta_curScopeId = nid
  End If
  Return prev
End Function

Private Function TaFindLabel(pos, slen As Integer) As Integer
  For i = 0 To ta_labelCount - 1 Do
    If SlicesEqual(ta_labels(i).namePos, ta_labels(i).nameLen, pos, slen) Then
      Return i
    End If
  End For
  Return -1
End Function

Private Sub TaCollectLabels(startIdx, cnt As Integer)
  For i = 0 To cnt - 1 Do
    Dim sid As Integer = g_kids(startIdx + i)
    Dim sk As Integer = g_nodes(sid).kind
    If sk = NdDefer Then
      ta_scopeDeferCnt(ta_curScopeId) = ta_scopeDeferCnt(ta_curScopeId) + 1
      ta_curDeferTotal = ta_curDeferTotal + 1
    End If
    If sk = NdLabel Then
      If TaFindLabel(g_nodes(sid).intVal, g_nodes(sid).lenVal) >= 0 Then
        Printf(MSG_TA_DUP_LABEL)
        TaPrintSlice(g_nodes(sid).intVal, g_nodes(sid).lenVal)
        Printf("'\n")
        ta_errCount = ta_errCount + 1
      Else
        If ta_labelCount < 128 Then
          ta_labels(ta_labelCount) = TaLabelInfo With (
            namePos = g_nodes(sid).intVal,
            nameLen = g_nodes(sid).lenVal,
            scopeId = ta_curScopeId,
            deferCnt = ta_scopeDeferCnt(ta_curScopeId),
            totalDefers = ta_curDeferTotal
          )
          ta_labelCount = ta_labelCount + 1
        End If
      End If
    End If
    If sk = NdIf Then
      Dim savedTot1 As Integer = ta_curDeferTotal
      Dim p1 As Integer = TaPushScope()
      TaCollectLabels(g_nodes(sid).firstChild, g_nodes(sid).childCount)
      ta_curScopeId = p1
      ta_curDeferTotal = savedTot1
      Dim savedTot2 As Integer = ta_curDeferTotal
      Dim p2 As Integer = TaPushScope()
      TaCollectLabels(g_nodes(sid).extraStart, g_nodes(sid).extraCount)
      ta_curScopeId = p2
      ta_curDeferTotal = savedTot2
    End If
    If (sk = NdWhile) Or (sk = NdFor) Or (sk = NdWith) Then
      Dim savedTot As Integer = ta_curDeferTotal
      Dim p1 As Integer = TaPushScope()
      TaCollectLabels(g_nodes(sid).firstChild, g_nodes(sid).childCount)
      ta_curScopeId = p1
      ta_curDeferTotal = savedTot
    End If
    If sk = NdMatch Then
      For bi = 0 To g_nodes(sid).childCount - 1 Do
        Dim arm As Integer = g_kids(g_nodes(sid).firstChild + bi)
        Dim savedTot As Integer = ta_curDeferTotal
        Dim p1 As Integer = TaPushScope()
        TaCollectLabels(g_nodes(arm).firstChild, g_nodes(arm).childCount)
        ta_curScopeId = p1
        ta_curDeferTotal = savedTot
      End For
    End If
  End For
End Sub

Private Sub TaCheckStmt(id As Integer)
  Dim k As Integer = g_nodes(id).kind

  If k = NdLabel Then
    If ta_deferDepth > 0 Then
      Printf(MSG_TA_DEFER_LABEL)
      ta_errCount = ta_errCount + 1
    End If
    Return
  End If

  If k = NdGoTo Then
    If ta_deferDepth > 0 Then
      Printf(MSG_TA_DEFER_GOTO)
      ta_errCount = ta_errCount + 1
      Return
    End If
    Dim li As Integer = TaFindLabel(g_nodes(id).intVal, g_nodes(id).lenVal)
    If li < 0 Then
      Printf(MSG_TA_UNDEF_LABEL)
      TaPrintSlice(g_nodes(id).intVal, g_nodes(id).lenVal)
      Printf("'\n")
      ta_errCount = ta_errCount + 1
    Else
      Dim targetScope As Integer = ta_labels(li).scopeId
      Dim targetDefCnt As Integer = ta_labels(li).deferCnt
      Dim walk As Integer = ta_curScopeId
      Dim isAnc As Boolean = False
      While walk >= 0 Do
        If walk = targetScope Then
          isAnc = True
          Break
        End If
        walk = ta_scopeParent(walk)
      End While
      If isAnc Then
        If ta_scopeDeferCnt(targetScope) <> targetDefCnt Then
          Printf(MSG_TA_DEFER_GOTO_CROSS)
          ta_errCount = ta_errCount + 1
        End If
      Else
        If (ta_curDeferTotal > 0) Or (ta_labels(li).totalDefers > 0) Then
          Printf(MSG_TA_DEFER_GOTO_CROSS)
          ta_errCount = ta_errCount + 1
        End If
      End If
    End If
    Return
  End If

  If k = NdBreak Then
    If ta_loopDepth <= 0 Then
      If ta_deferDepth > 0 Then
        Printf(MSG_TA_DEFER_BREAK)
      Else
        Printf(MSG_TA_BREAK_LOOP)
      End If
      ta_errCount = ta_errCount + 1
    End If
    Return
  End If

  If k = NdContinue Then
    If ta_loopDepth <= 0 Then
      If ta_deferDepth > 0 Then
        Printf(MSG_TA_DEFER_CONTINUE)
      Else
        Printf(MSG_TA_CONTINUE_LOOP)
      End If
      ta_errCount = ta_errCount + 1
    End If
    Return
  End If

  If k = NdDefer Then
    ta_scopeDeferCnt(ta_curScopeId) = ta_scopeDeferCnt(ta_curScopeId) + 1
    ta_curDeferTotal = ta_curDeferTotal + 1
    Dim savedLocals = ta_localCount, savedLoop = ta_loopDepth, savedScope = ta_curScopeId, savedTot = ta_curDeferTotal As Integer
    ta_loopDepth = 0
    ta_deferDepth = ta_deferDepth + 1
    For di = 0 To g_nodes(id).childCount - 1 Do
      TaCheckStmt(g_kids(g_nodes(id).firstChild + di))
    End For
    ta_deferDepth = ta_deferDepth - 1
    ta_loopDepth = savedLoop
    ta_localCount = savedLocals
    ta_curScopeId = savedScope
    ta_curDeferTotal = savedTot
    Return
  End If

  If k = NdDim Then
    Dim nPos As Integer = g_nodes(id).intVal
    Dim nLen As Integer = g_nodes(id).lenVal
    If TaFindLocal(nPos, nLen) >= 0 Then
      Printf("Semantic Type Error: Variable '")
      TaPrintSlice(nPos, nLen)
      Printf("' is already declared\n")
      ta_errCount = ta_errCount + 1
    End If
    Dim ty As TypeInfo = TaResolveTypeEx(g_nodes(id).tyKind, g_nodes(id).tyInnerKind, g_nodes(id).tyArrLen, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    If g_nodes(id).right >= 0 Then
      Printf(MSG_TA_AT_NON_STATIC)
      ta_errCount = ta_errCount + 1
    End If
    Dim initId As Integer = g_nodes(id).left
    If (ty.arrLen = 0) And (ty.kind = TyPointer) And (initId >= 0) Then
      Dim ik As Integer = g_nodes(initId).kind
      If (ik = NdCall) Or (ik = NdStructInit) Then
        Dim sIdx As Integer = TaFindStruct(g_nodes(initId).intVal, g_nodes(initId).lenVal)
        If sIdx >= 0 Then
          ty = TypeInfo With ( kind = TyStruct, typeIdx = sIdx, arrLen = 0, namePos = g_nodes(initId).intVal, nameLen = g_nodes(initId).lenVal )
        End If
      End If
    End If
    TaValidateType(ty)
    If initId >= 0 Then
      If g_nodes(initId).kind = NdArrayLit Then
        If ty.arrLen > 0 Then
          Dim litCnt As Integer = g_nodes(initId).childCount
          If litCnt > ty.arrLen Then
            Printf("Semantic Type Error: Array initializer for '")
            TaPrintSlice(nPos, nLen)
            Printf("' has %d elements, exceeding size %d\n", litCnt, ty.arrLen)
            ta_errCount = ta_errCount + 1
          End If
          Dim elemTy As TypeInfo With ( kind = ty.kind, typeIdx = ty.typeIdx, arrLen = 0, namePos = ty.namePos, nameLen = ty.nameLen, innerKind = ty.innerKind )
          For ei = 0 To litCnt - 1 Do
            Dim eNode As Integer = g_kids(g_nodes(initId).firstChild + ei)
            Dim eTy As TypeInfo = TaTypeOfExpr(eNode)
            If Not TaIsAssignable(elemTy, eTy, eNode) Then
              Printf("Semantic Type Error: Array element type mismatch for '")
              TaPrintSlice(nPos, nLen)
              Printf("'\n")
              ta_errCount = ta_errCount + 1
            End If
          End For
        Else
          Printf("Semantic Type Error: Array literal can only initialize a fixed-size array\n")
          ta_errCount = ta_errCount + 1
        End If
      Else
        Dim actualTy As TypeInfo = TaTypeOfExpr(initId)
        If Not TaIsAssignable(ty, actualTy, initId) Then
          Printf("Semantic Type Error: Variable declaration assignment mismatch for '")
          TaPrintSlice(nPos, nLen)
          Printf("'\n")
          ta_errCount = ta_errCount + 1
        End If
      End If
    End If
    TaAccountStackLocal(nPos, nLen, ty)
    ta_locals(ta_localCount) = TaLocalSym With ( namePos = nPos, nameLen = nLen, ty = ty )
    ta_localCount = ta_localCount + 1
    Return
  End If

  If k = NdStaticDim Then
    Dim nPos As Integer = g_nodes(id).intVal
    Dim nLen As Integer = g_nodes(id).lenVal
    If TaFindLocal(nPos, nLen) >= 0 Then
      Printf("Semantic Type Error: Variable '")
      TaPrintSlice(nPos, nLen)
      Printf("' is already declared\n")
      ta_errCount = ta_errCount + 1
    End If
    Dim ty As TypeInfo = TaResolveTypeEx(g_nodes(id).tyKind, g_nodes(id).tyInnerKind, g_nodes(id).tyArrLen, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    TaValidateType(ty)
    If g_nodes(id).right >= 0 Then
      Printf(MSG_TA_AT_NON_STATIC)
      ta_errCount = ta_errCount + 1
    End If
    Dim initId As Integer = g_nodes(id).left
    If initId >= 0 Then
      TaTypeOfExpr(initId)
      If Not TaIsConstExpr(ty, initId) Then
        Printf("Semantic Type Error: Static variable initializer must be a compile-time constant\n")
        ta_errCount = ta_errCount + 1
      End If
    End If
    ta_locals(ta_localCount) = TaLocalSym With ( namePos = nPos, nameLen = nLen, ty = ty )
    ta_localCount = ta_localCount + 1
    Return
  End If

  If k = NdAssign Then
    Dim nPos As Integer = g_nodes(id).intVal
    Dim nLen As Integer = g_nodes(id).lenVal
    Dim targetTy As TypeInfo
    If Not TaLookupVar(nPos, nLen, ByRef targetTy) Then
      Printf("Semantic Type Error: Assignment target variable '")
      TaPrintSlice(nPos, nLen)
      Printf("' is undefined\n")
      ta_errCount = ta_errCount + 1
      Return
    End If
    Dim valTy As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
    If Not TaIsAssignable(targetTy, valTy, g_nodes(id).left) Then
      Printf("Semantic Type Error: Type mismatch assignment on variable '")
      TaPrintSlice(nPos, nLen)
      Printf("'\n")
      ta_errCount = ta_errCount + 1
    End If
    Return
  End If

  If k = NdFieldAssign Then
    Dim baseTy As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
    Dim fPos As Integer = g_nodes(id).intVal
    Dim fLen As Integer = g_nodes(id).lenVal
    Dim targetTy As TypeInfo = TaMakePrim(TyInt)
    If (baseTy.arrLen = 0) And (baseTy.kind = TyStruct) And (baseTy.typeIdx >= 0) Then
      Dim fIdx As Integer = TaFindStructField(baseTy.typeIdx, fPos, fLen, ByRef targetTy)
      If fIdx < 0 Then
        Printf("Semantic Type Error: Structure '")
        TaPrintSlice(baseTy.namePos, baseTy.nameLen)
        Printf("' has no field named '")
        TaPrintSlice(fPos, fLen)
        Printf("'\n")
        ta_errCount = ta_errCount + 1
        Return
      End If
    Else
      Printf("Semantic Type Error: Field access requires a structure value before .")
      TaPrintSlice(fPos, fLen)
      Printf("\n")
      ta_errCount = ta_errCount + 1
      Return
    End If
    Dim valTy As TypeInfo = TaTypeOfExpr(g_nodes(id).right)
    If Not TaIsAssignable(targetTy, valTy, g_nodes(id).right) Then
      Printf("Semantic Type Error: Type assignment mismatch inside field '")
      TaPrintSlice(fPos, fLen)
      Printf("'\n")
      ta_errCount = ta_errCount + 1
    End If
    Return
  End If

  If k = NdIndexAssign Then
    Dim baseTy As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
    Dim elemTy As TypeInfo = TaCheckIndex(baseTy, g_nodes(id).right)
    Dim valTy As TypeInfo = TaTypeOfExpr(g_nodes(id).third)
    If Not TaIsAssignable(elemTy, valTy, g_nodes(id).third) Then
      Printf("Semantic Type Error: Type assignment mismatch in array index assignment\n")
      ta_errCount = ta_errCount + 1
    End If
    Return
  End If

  If k = NdExprStmt Then
    TaTypeOfExpr(g_nodes(id).left)
    Return
  End If

  If k = NdReturn Then
    If ta_deferDepth > 0 Then
      Printf(MSG_TA_DEFER_RETURN)
      ta_errCount = ta_errCount + 1
      Return
    End If
    Dim exprId As Integer = g_nodes(id).left
    If exprId < 0 Then
      If ta_curRetTy.kind <> TyNothing Then
        Printf("Semantic Type Error: Function must return a value of type ")
        TaPrintType(ta_curRetTy)
        Printf("\n")
        ta_errCount = ta_errCount + 1
      End If
    Else
      Dim actualTy As TypeInfo = TaTypeOfExpr(exprId)
      If ta_curRetTy.kind = TyNothing Then
        Printf("Semantic Type Error: Sub cannot return a value\n")
        ta_errCount = ta_errCount + 1
      Else
        If Not TaIsAssignable(ta_curRetTy, actualTy, exprId) Then
          Printf("Semantic Type Error: Return type mismatch: expected ")
          TaPrintType(ta_curRetTy)
          Printf(", got ")
          TaPrintType(actualTy)
          Printf("\n")
          ta_errCount = ta_errCount + 1
        End If
      End If
    End If
    Return
  End If

  If k = NdAssert Then
    Dim condTy As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
    If Not (((condTy.arrLen = 0) And (condTy.kind = TyBoolean)) Or TaIsIntType(condTy)) Then
      Printf("Semantic Type Error: Assert condition must be Boolean, got ")
      TaPrintType(condTy)
      Printf("\n")
      ta_errCount = ta_errCount + 1
    End If
    If g_nodes(id).right >= 0 Then
      Dim msgTy As TypeInfo = TaTypeOfExpr(g_nodes(id).right)
      If Not ((msgTy.arrLen = 0) And (msgTy.kind = TyPointer)) Then
        Printf("Semantic Type Error: Assert message must be a string Pointer, got ")
        TaPrintType(msgTy)
        Printf("\n")
        ta_errCount = ta_errCount + 1
      End If
    End If
    Return
  End If

  If k = NdIf Then
    Dim condTy As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
    If Not (((condTy.arrLen = 0) And (condTy.kind = TyBoolean)) Or TaIsIntType(condTy)) Then
      Printf("Semantic Type Error: If condition must be Boolean, got ")
      TaPrintType(condTy)
      Printf("\n")
      ta_errCount = ta_errCount + 1
    End If
    Dim savedLocals As Integer = ta_localCount
    Dim savedTot1 As Integer = ta_curDeferTotal
    Dim prevScope1 As Integer = TaPushScope()
    Dim i As Integer = 0
    While i < g_nodes(id).childCount Do
      TaCheckStmt(g_kids(g_nodes(id).firstChild + i))
      i = i + 1
    End While
    ta_curScopeId = prevScope1
    ta_curDeferTotal = savedTot1
    ta_localCount = savedLocals
    Dim savedTot2 As Integer = ta_curDeferTotal
    Dim prevScope2 As Integer = TaPushScope()
    i = 0
    While i < g_nodes(id).extraCount Do
      TaCheckStmt(g_kids(g_nodes(id).extraStart + i))
      i = i + 1
    End While
    ta_curScopeId = prevScope2
    ta_curDeferTotal = savedTot2
    ta_localCount = savedLocals
    Return
  End If

  If k = NdWhile Then
    Dim condTy As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
    If Not (((condTy.arrLen = 0) And (condTy.kind = TyBoolean)) Or TaIsIntType(condTy)) Then
      Printf("Semantic Type Error: While condition must be Boolean, got ")
      TaPrintType(condTy)
      Printf("\n")
      ta_errCount = ta_errCount + 1
    End If
    Dim savedLocals As Integer = ta_localCount
    Dim savedTot As Integer = ta_curDeferTotal
    Dim prevScope As Integer = TaPushScope()
    ta_loopDepth = ta_loopDepth + 1
    Dim i As Integer = 0
    While i < g_nodes(id).childCount Do
      TaCheckStmt(g_kids(g_nodes(id).firstChild + i))
      i = i + 1
    End While
    ta_loopDepth = ta_loopDepth - 1
    ta_curScopeId = prevScope
    ta_curDeferTotal = savedTot
    ta_localCount = savedLocals
    Return
  End If

  If k = NdFor Then
    Dim stTy As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
    Dim enTy As TypeInfo = TaTypeOfExpr(g_nodes(id).right)
    If (Not TaIsIntType(stTy)) Or (Not TaIsIntType(enTy)) Then
      Printf("Semantic Type Error: For loop bounds must be integer types\n")
      ta_errCount = ta_errCount + 1
    End If
    Dim savedLocals As Integer = ta_localCount
    Dim savedTot As Integer = ta_curDeferTotal
    Dim prevScope As Integer = TaPushScope()
    ta_locals(ta_localCount) = TaLocalSym With ( namePos = g_nodes(id).intVal, nameLen = g_nodes(id).lenVal, ty = TaMakePrim(TyInt) )
    ta_localCount = ta_localCount + 1
    ta_loopDepth = ta_loopDepth + 1
    For i = 0 To g_nodes(id).childCount - 1 Do
      TaCheckStmt(g_kids(g_nodes(id).firstChild + i))
    End For
    ta_loopDepth = ta_loopDepth - 1
    ta_curScopeId = prevScope
    ta_curDeferTotal = savedTot
    ta_localCount = savedLocals
    Return
  End If

  If k = NdWith Then
    Dim tgt As Integer = g_nodes(id).left
    Dim tgtTy As TypeInfo = TaTypeOfExpr(tgt)
    If Not TaIsLValue(tgt) Then
      Printf("Semantic Type Error: With target must be a variable, struct field, or array element\n")
      ta_errCount = ta_errCount + 1
    Else
      If (tgtTy.arrLen <> 0) Or (tgtTy.kind <> TyStruct) Then
        Printf("Semantic Type Error: With target must be a Structure, got ")
        TaPrintType(tgtTy)
        Printf("\n")
        ta_errCount = ta_errCount + 1
      End If
    End If
    If ta_withDepth < 16 Then
      ta_withTys(ta_withDepth) = tgtTy
      ta_withDepth = ta_withDepth + 1
      Dim savedLocals As Integer = ta_localCount
      Dim savedTot As Integer = ta_curDeferTotal
      Dim prevScope As Integer = TaPushScope()
      For wi = 0 To g_nodes(id).childCount - 1 Do
        TaCheckStmt(g_kids(g_nodes(id).firstChild + wi))
      End For
      ta_curScopeId = prevScope
      ta_curDeferTotal = savedTot
      ta_localCount = savedLocals
      ta_withDepth = ta_withDepth - 1
    End If
    Return
  End If

  If k = NdMatch Then
    Dim targetTy As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
    If (g_nodes(id).op = 0) And (targetTy.arrLen = 0) And (targetTy.kind = TyUnion) And (targetTy.typeIdx >= 0) Then
      Dim uIdx As Integer = targetTy.typeIdx
      Dim vCount As Integer = ta_unions(uIdx).varCount
      Dim covered(128) As Integer
      For ci = 0 To 127 Do
        covered(ci) = 0
      End For
      Dim hasWild As Boolean = False

      For bi = 0 To g_nodes(id).childCount - 1 Do
        Dim arm As Integer = g_kids(g_nodes(id).firstChild + bi)
        Dim hasGuard As Boolean = (g_nodes(arm).left >= 0)
        Dim savedLocals As Integer = ta_localCount

        If g_nodes(arm).op = 1 Then
          If Not hasGuard Then
            hasWild = True
          End If
        Else
          Dim pCount As Integer = g_nodes(arm).extraCount
          For vi = 0 To pCount - 1 Do
            Dim pe As Integer = g_kids(g_nodes(arm).extraStart + vi)
            Dim pk As Integer = g_nodes(pe).kind
            Dim qPos = 0, qLen = 0, vPos = 0, vLen = 0 As Integer
            Dim bindNodes(32) As Integer
            Dim bindCnt As Integer = 0
            Dim patOk As Boolean = True

            If pk = NdVar Then
              vPos = g_nodes(pe).intVal
              vLen = g_nodes(pe).lenVal
            Else
              If pk = NdCall Then
                vPos = g_nodes(pe).intVal
                vLen = g_nodes(pe).lenVal
                Dim ac As Integer = g_nodes(pe).childCount
                For ai = 0 To ac - 1 Do
                  Dim aNode As Integer = g_kids(g_nodes(pe).firstChild + ai)
                  If g_nodes(aNode).op = 1 Then
                    Printf("Semantic Type Error: Match variant pattern binding cannot use ByRef\n")
                    ta_errCount = ta_errCount + 1
                  End If
                  If bindCnt < 32 Then
                    bindNodes(bindCnt) = g_nodes(aNode).left
                    bindCnt = bindCnt + 1
                  End If
                End For
              Else
                If pk = NdFieldAccess Then
                  Dim bNode As Integer = g_nodes(pe).left
                  If (bNode >= 0) And (g_nodes(bNode).kind = NdVar) Then
                    qPos = g_nodes(bNode).intVal
                    qLen = g_nodes(bNode).lenVal
                    vPos = g_nodes(pe).intVal
                    vLen = g_nodes(pe).lenVal
                  Else
                    patOk = False
                  End If
                Else
                  If pk = NdIndexAccess Then
                    Dim faNode As Integer = g_nodes(pe).left
                    If (faNode >= 0) And (g_nodes(faNode).kind = NdFieldAccess) Then
                      Dim bNode As Integer = g_nodes(faNode).left
                      If (bNode >= 0) And (g_nodes(bNode).kind = NdVar) Then
                        qPos = g_nodes(bNode).intVal
                        qLen = g_nodes(bNode).lenVal
                        vPos = g_nodes(faNode).intVal
                        vLen = g_nodes(faNode).lenVal
                        bindNodes(0) = g_nodes(pe).right
                        bindCnt = 1
                      Else
                        patOk = False
                      End If
                    Else
                      patOk = False
                    End If
                  Else
                    If pk = NdUnionInit Then
                      Dim bNode As Integer = g_nodes(pe).left
                      If (bNode >= 0) And (g_nodes(bNode).kind = NdVar) Then
                        qPos = g_nodes(bNode).intVal
                        qLen = g_nodes(bNode).lenVal
                        vPos = g_nodes(pe).intVal
                        vLen = g_nodes(pe).lenVal
                        Dim ac As Integer = g_nodes(pe).childCount
                        For ai = 0 To ac - 1 Do
                          If bindCnt < 32 Then
                            bindNodes(bindCnt) = g_kids(g_nodes(pe).firstChild + ai)
                            bindCnt = bindCnt + 1
                          End If
                        End For
                      Else
                        patOk = False
                      End If
                    Else
                      patOk = False
                    End If
                  End If
                End If
              End If
            End If

            If Not patOk Then
              Printf("Semantic Type Error: Invalid Union Case pattern in Match\n")
              ta_errCount = ta_errCount + 1
            Else
              If (qLen > 0) And (Not SlicesEqual(qPos, qLen, ta_unions(uIdx).namePos, ta_unions(uIdx).nameLen)) Then
                Printf("Semantic Type Error: Match Case union '")
                TaPrintSlice(qPos, qLen)
                Printf("' does not match scrutinee union '")
                TaPrintSlice(ta_unions(uIdx).namePos, ta_unions(uIdx).nameLen)
                Printf("'\n")
                ta_errCount = ta_errCount + 1
              End If
              Dim vIdx As Integer = TaFindUnionVariant(uIdx, vPos, vLen)
              If vIdx < 0 Then
                Printf("Semantic Type Error: Union '")
                TaPrintSlice(ta_unions(uIdx).namePos, ta_unions(uIdx).nameLen)
                Printf("' has no variant named '")
                TaPrintSlice(vPos, vLen)
                Printf("'\n")
                ta_errCount = ta_errCount + 1
              Else
                Dim vInfo As TaUnionVar = ta_unionVars(ta_unions(uIdx).varStart + vIdx)
                If bindCnt <> vInfo.fieldCount Then
                  Printf("Semantic Type Error: Variant '")
                  TaPrintSlice(vPos, vLen)
                  Printf("' expects %d payload binding(s), got %d (use '_' to ignore)\n", vInfo.fieldCount, bindCnt)
                  ta_errCount = ta_errCount + 1
                End If
                Dim lim As Integer = bindCnt
                If vInfo.fieldCount < lim Then
                  lim = vInfo.fieldCount
                End If
                For pbi = 0 To lim - 1 Do
                  Dim bn As Integer = bindNodes(pbi)
                  If (bn < 0) Or (g_nodes(bn).kind <> NdVar) Then
                    Printf("Semantic Type Error: Match variant payload binding must be an identifier or '_'\n")
                    ta_errCount = ta_errCount + 1
                  Else
                    Dim bp = g_nodes(bn).intVal, bl = g_nodes(bn).lenVal As Integer
                    Dim isIgnore As Boolean = (bl = 1) And (g_srcPool(bp) = 95)
                    If Not isIgnore Then
                      If pCount > 1 Then
                        Printf(MSG_TA_UNION_PAT_MULTI_BIND)
                        ta_errCount = ta_errCount + 1
                      Else
                        For prevB = 0 To pbi - 1 Do
                          Dim pbn As Integer = bindNodes(prevB)
                          If (pbn >= 0) And (g_nodes(pbn).kind = NdVar) Then
                            If SlicesEqual(g_nodes(pbn).intVal, g_nodes(pbn).lenVal, bp, bl) Then
                              Printf("Semantic Type Error: Duplicate binding name '")
                              TaPrintSlice(bp, bl)
                              Printf("' in Match Case pattern\n")
                              ta_errCount = ta_errCount + 1
                            End If
                          End If
                        End For
                        Dim fTy As TypeInfo = ta_fields(vInfo.fieldStart + pbi).ty
                        TaAccountStackLocal(bp, bl, fTy)
                        ta_locals(ta_localCount) = TaLocalSym With ( namePos = bp, nameLen = bl, ty = fTy )
                        ta_localCount = ta_localCount + 1
                      End If
                    End If
                  End If
                End For
                If (Not hasGuard) And (vIdx < 128) Then
                  covered(vIdx) = 1
                End If
              End If
            End If
          End For
        End If

        If hasGuard Then
          Dim gTy As TypeInfo = TaTypeOfExpr(g_nodes(arm).left)
          If Not (((gTy.arrLen = 0) And (gTy.kind = TyBoolean)) Or TaIsIntType(gTy)) Then
            Printf(MSG_TA_MATCH_GUARD)
            ta_errCount = ta_errCount + 1
          End If
        End If
        Dim savedTot As Integer = ta_curDeferTotal
        Dim prevScope As Integer = TaPushScope()
        For si = 0 To g_nodes(arm).childCount - 1 Do
          TaCheckStmt(g_kids(g_nodes(arm).firstChild + si))
        End For
        ta_curScopeId = prevScope
        ta_curDeferTotal = savedTot
        ta_localCount = savedLocals
      End For

      If Not hasWild Then
        Dim missCnt As Integer = 0
        For vi = 0 To vCount - 1 Do
          If (vi < 128) And (covered(vi) = 0) Then
            If missCnt = 0 Then
              Printf("Semantic Type Error: Non-exhaustive Match on Union '")
              TaPrintSlice(ta_unions(uIdx).namePos, ta_unions(uIdx).nameLen)
              Printf("'; missing variant(s): ")
            Else
              Printf(", ")
            End If
            Dim mv As TaUnionVar = ta_unionVars(ta_unions(uIdx).varStart + vi)
            TaPrintSlice(mv.namePos, mv.nameLen)
            missCnt = missCnt + 1
          End If
        End For
        If missCnt > 0 Then
          Printf("\n")
          ta_errCount = ta_errCount + 1
        End If
      End If
      Return
    End If

    For bi = 0 To g_nodes(id).childCount - 1 Do
      Dim arm As Integer = g_kids(g_nodes(id).firstChild + bi)
      If g_nodes(arm).op = 0 Then
        For vi = 0 To g_nodes(arm).extraCount - 1 Do
          Dim pe As Integer = g_kids(g_nodes(arm).extraStart + vi)
          Dim pTy As TypeInfo = TaTypeOfExpr(pe)
          Dim okPat As Boolean = TaIsAssignable(targetTy, pTy, pe) Or (TaIsStrLike(targetTy) And TaIsStrLike(pTy))
          If Not okPat Then
            Printf(MSG_TA_MATCH_TYPE)
            ta_errCount = ta_errCount + 1
          End If
        End For
      End If
      If g_nodes(arm).left >= 0 Then
        Dim gTy As TypeInfo = TaTypeOfExpr(g_nodes(arm).left)
        If Not (((gTy.arrLen = 0) And (gTy.kind = TyBoolean)) Or TaIsIntType(gTy)) Then
          Printf(MSG_TA_MATCH_GUARD)
          ta_errCount = ta_errCount + 1
        End If
      End If
      Dim savedLocals As Integer = ta_localCount
      Dim savedTot As Integer = ta_curDeferTotal
      Dim prevScope As Integer = TaPushScope()
      For si = 0 To g_nodes(arm).childCount - 1 Do
        TaCheckStmt(g_kids(g_nodes(arm).firstChild + si))
      End For
      ta_curScopeId = prevScope
      ta_curDeferTotal = savedTot
      ta_localCount = savedLocals
    End For
    Return
  End If
End Sub

Public Function AnalyzeProgram(ByRef arena As Arena, progRoot As Integer) As Integer
  ta_enums = ArenaAlloc(ByRef arena, TaEnumInfo, 128)
  ta_enumMems = ArenaAlloc(ByRef arena, TaEnumMem, 1024)
  ta_structs = ArenaAlloc(ByRef arena, TaStructInfo, 512)
  ta_fields = ArenaAlloc(ByRef arena, TaFieldInfo, 4096)
  ta_unions = ArenaAlloc(ByRef arena, TaUnionInfo, 512)
  ta_unionVars = ArenaAlloc(ByRef arena, TaUnionVar, 4096)
  ta_globals = ArenaAlloc(ByRef arena, TaGlobalInfo, 1024)
  ta_funcs = ArenaAlloc(ByRef arena, TaFuncInfo, 2048)
  ta_ops = ArenaAlloc(ByRef arena, TaOpInfo, 512)
  ta_params = ArenaAlloc(ByRef arena, TaParamInfo, 8192)
  ta_fpSigs = ArenaAlloc(ByRef arena, TaFpSig, 1024)
  ta_fpParams = ArenaAlloc(ByRef arena, TaParamInfo, 4096)
  ta_locals = ArenaAlloc(ByRef arena, TaLocalSym, 512)
  ta_scopeParent = ArenaAlloc(ByRef arena, Integer, 4096)
  ta_scopeDeferCnt = ArenaAlloc(ByRef arena, Integer, 4096)

  ta_enumCount = 0
  ta_enumMemCount = 0
  ta_structCount = 0
  ta_fieldCount = 0
  ta_unionCount = 0
  ta_unionVarCount = 0
  ta_globalCount = 0
  ta_funcCount = 0
  ta_opCount = 0
  ta_paramCount = 0
  ta_fpSigCount = 0
  ta_fpParamCount = 0
  ta_localCount = 0
  ta_withDepth = 0
  ta_loopDepth = 0
  ta_errCount = 0

  Dim defStart As Integer = g_nodes(progRoot).firstChild
  Dim defCount As Integer = g_nodes(progRoot).childCount

  ' Pass 1a: Register Enums and Struct headers
  For i = 0 To defCount - 1 Do
    Dim d As Integer = g_kids(defStart + i)
    Dim dk As Integer = g_nodes(d).kind
    If dk = NdEnumDef Then
      Dim ms As Integer = ta_enumMemCount
      Dim mc As Integer = g_nodes(d).childCount
      Dim j As Integer = 0
      While j < mc Do
        Dim m As Integer = g_kids(g_nodes(d).firstChild + j)
        ta_enumMems(ta_enumMemCount) = TaEnumMem With ( namePos = g_nodes(m).intVal, nameLen = g_nodes(m).lenVal )
        ta_enumMemCount = ta_enumMemCount + 1
        j = j + 1
      End While
      Dim isPriv As Integer = 0
      If g_nodes(d).op = 2 Then
        isPriv = 1
      End If
      ta_enums(ta_enumCount) = TaEnumInfo With (
        namePos = g_nodes(d).intVal,
        nameLen = g_nodes(d).lenVal,
        memStart = ms,
        memCount = mc,
        isPrivate = isPriv,
        modId = g_nodes(d).modId
      )
      ta_enumCount = ta_enumCount + 1
    End If
    If dk = NdStructDef Then
      Dim isPriv As Integer = 0
      If g_nodes(d).op = 2 Then
        isPriv = 1
      End If
      ta_structs(ta_structCount) = TaStructInfo With (
        namePos = g_nodes(d).intVal,
        nameLen = g_nodes(d).lenVal,
        fieldStart = 0,
        fieldCount = 0,
        isPrivate = isPriv,
        modId = g_nodes(d).modId
      )
      ta_structCount = ta_structCount + 1
    End If
    If dk = NdUnionDef Then
      Dim isPriv As Integer = 0
      If g_nodes(d).op = 2 Then
        isPriv = 1
      End If
      If g_nodes(d).childCount <= 0 Then
        Printf(MSG_TA_UNION_EMPTY)
        ta_errCount = ta_errCount + 1
      End If
      ta_unions(ta_unionCount) = TaUnionInfo With (
        namePos = g_nodes(d).intVal,
        nameLen = g_nodes(d).lenVal,
        varStart = 0,
        varCount = 0,
        isPrivate = isPriv,
        modId = g_nodes(d).modId
      )
      ta_unionCount = ta_unionCount + 1
    End If
  End For

  ' Pass 1b: Resolve Struct fields and Union variant fields
  Dim si As Integer = 0
  Dim ui As Integer = 0
  For i = 0 To defCount - 1 Do
    Dim d As Integer = g_kids(defStart + i)
    If g_nodes(d).kind = NdStructDef Then
      ta_curModId = g_nodes(d).modId
      ta_altModId = g_nodes(d).third
      Dim fs As Integer = ta_fieldCount
      Dim fc As Integer = g_nodes(d).childCount
      Dim j As Integer = 0
      While j < fc Do
        Dim f As Integer = g_kids(g_nodes(d).firstChild + j)
        Dim fty As TypeInfo = TaResolveTypeEx(g_nodes(f).tyKind, g_nodes(f).tyInnerKind, g_nodes(f).tyArrLen, g_nodes(f).tyNamePos, g_nodes(f).tyNameLen)
        TaValidateType(fty)
        ta_fields(ta_fieldCount) = TaFieldInfo With (
          namePos = g_nodes(f).intVal,
          nameLen = g_nodes(f).lenVal,
          ty = fty
        )
        ta_fieldCount = ta_fieldCount + 1
        j = j + 1
      End While
      ta_structs(si).fieldStart = fs
      ta_structs(si).fieldCount = fc
      si = si + 1
    End If
    If g_nodes(d).kind = NdUnionDef Then
      ta_curModId = g_nodes(d).modId
      ta_altModId = g_nodes(d).third
      Dim vs As Integer = ta_unionVarCount
      Dim vc As Integer = g_nodes(d).childCount
      For vi = 0 To vc - 1 Do
        Dim vn As Integer = g_kids(g_nodes(d).firstChild + vi)
        For prevV = 0 To vi - 1 Do
          Dim pvn As Integer = g_kids(g_nodes(d).firstChild + prevV)
          If SlicesEqual(g_nodes(pvn).intVal, g_nodes(pvn).lenVal, g_nodes(vn).intVal, g_nodes(vn).lenVal) Then
            Printf(MSG_TA_UNION_DUP_VAR)
            ta_errCount = ta_errCount + 1
          End If
        End For
        Dim fs As Integer = ta_fieldCount
        Dim fc As Integer = g_nodes(vn).childCount
        For fi2 = 0 To fc - 1 Do
          Dim f As Integer = g_kids(g_nodes(vn).firstChild + fi2)
          If g_nodes(f).op = 1 Then
            Printf(MSG_TA_UNION_BYREF)
            ta_errCount = ta_errCount + 1
          End If
          Dim fty As TypeInfo = TaResolveTypeEx(g_nodes(f).tyKind, g_nodes(f).tyInnerKind, g_nodes(f).tyArrLen, g_nodes(f).tyNamePos, g_nodes(f).tyNameLen)
          TaValidateType(fty)
          ta_fields(ta_fieldCount) = TaFieldInfo With (
            namePos = g_nodes(f).intVal,
            nameLen = g_nodes(f).lenVal,
            ty = fty
          )
          ta_fieldCount = ta_fieldCount + 1
        End For
        ta_unionVars(ta_unionVarCount) = TaUnionVar With (
          namePos = g_nodes(vn).intVal,
          nameLen = g_nodes(vn).lenVal,
          fieldStart = fs,
          fieldCount = fc
        )
        ta_unionVarCount = ta_unionVarCount + 1
      End For
      ta_unions(ui).varStart = vs
      ta_unions(ui).varCount = vc
      ui = ui + 1
    End If
  End For
  ta_altModId = -1

  ' Pass 1c: Register Globals, Functions, and ExternDecls
  For i = 0 To defCount - 1 Do
    Dim d As Integer = g_kids(defStart + i)
    Dim dk As Integer = g_nodes(d).kind
    ta_curModId = g_nodes(d).modId
    ta_altModId = g_nodes(d).third
    If dk = NdGlobalVar Then
      Dim isPriv As Integer = 0
      If g_nodes(d).op = 2 Then
        isPriv = 1
      End If
      Dim gty As TypeInfo = TaResolveTypeEx(g_nodes(d).tyKind, g_nodes(d).tyInnerKind, g_nodes(d).tyArrLen, g_nodes(d).tyNamePos, g_nodes(d).tyNameLen)
      ta_globals(ta_globalCount) = TaGlobalInfo With (
        namePos = g_nodes(d).intVal,
        nameLen = g_nodes(d).lenVal,
        isPrivate = isPriv,
        modId = g_nodes(d).modId,
        ty = gty,
        initNode = g_nodes(d).left
      )
      ta_globalCount = ta_globalCount + 1
    End If
    If (dk = NdFuncDef) Or (dk = NdExternDecl) Then
      Dim ps As Integer = ta_paramCount
      Dim pc As Integer = g_nodes(d).extraCount
      Dim j As Integer = 0
      While j < pc Do
        Dim pn As Integer = g_kids(g_nodes(d).extraStart + j)
        Dim pty As TypeInfo = TaResolveTypeEx(g_nodes(pn).tyKind, g_nodes(pn).tyInnerKind, g_nodes(pn).tyArrLen, g_nodes(pn).tyNamePos, g_nodes(pn).tyNameLen)
        ta_params(ta_paramCount) = TaParamInfo With ( isByRef = g_nodes(pn).op, ty = pty )
        ta_paramCount = ta_paramCount + 1
        j = j + 1
      End While
      Dim isExt = 0, isVar = 0, isPriv = 0, isAsy = 0 As Integer
      If dk = NdExternDecl Then
        isExt = 1
        isVar = g_nodes(d).op
      Else
        If g_nodes(d).op = 2 Then
          isPriv = 1
        End If
        If g_nodes(d).flags = 1 Then
          isAsy = 1
        End If
      End If
      Dim bRetTy As TypeInfo = TaResolveTypeEx(g_nodes(d).tyKind, g_nodes(d).tyInnerKind, 0, g_nodes(d).tyNamePos, g_nodes(d).tyNameLen)
      Dim cRetTy As TypeInfo = bRetTy
      If isAsy = 1 Then
        If SliceEqualsStr(g_nodes(d).intVal, g_nodes(d).lenVal, "main", 4) Then
          Printf(MSG_TA_ASYNC_MAIN)
          ta_errCount = ta_errCount + 1
        End If
        cRetTy = TypeInfo With (
          kind = TyTask,
          typeIdx = bRetTy.typeIdx,
          arrLen = 0,
          namePos = bRetTy.namePos,
          nameLen = bRetTy.nameLen,
          innerKind = bRetTy.kind
        )
      End If
      ta_funcs(ta_funcCount) = TaFuncInfo With (
        namePos = g_nodes(d).intVal,
        nameLen = g_nodes(d).lenVal,
        isExtern = isExt,
        isVariadic = isVar,
        isPrivate = isPriv,
        isAsync = isAsy,
        modId = g_nodes(d).modId,
        retTy = cRetTy,
        bodyRetTy = bRetTy,
        paramStart = ps,
        paramCount = pc
      )
      ta_funcCount = ta_funcCount + 1
    End If
    If dk = NdOpDef Then
      Dim ops As Integer = ta_paramCount
      Dim opc As Integer = g_nodes(d).extraCount
      Dim opTok As Integer = g_nodes(d).intVal
      Dim hasStructParam As Boolean = False
      For j = 0 To opc - 1 Do
        Dim pn As Integer = g_kids(g_nodes(d).extraStart + j)
        If g_nodes(pn).op = 1 Then
          Printf(MSG_TA_OP_BYREF)
          ta_errCount = ta_errCount + 1
        End If
        Dim pty As TypeInfo = TaResolveTypeEx(g_nodes(pn).tyKind, g_nodes(pn).tyInnerKind, g_nodes(pn).tyArrLen, g_nodes(pn).tyNamePos, g_nodes(pn).tyNameLen)
        If (pty.arrLen = 0) And ((pty.kind = TyStruct) Or (pty.kind = TyUnion)) Then
          hasStructParam = True
        End If
        ta_params(ta_paramCount) = TaParamInfo With ( isByRef = g_nodes(pn).op, ty = pty )
        ta_paramCount = ta_paramCount + 1
      End For
      Dim arityOk As Boolean = False
      If (opTok = TkNot) And (opc = 1) Then
        arityOk = True
      Else
        If (opTok = TkMinus) And ((opc = 1) Or (opc = 2)) Then
          arityOk = True
        Else
          If (opTok <> TkNot) And (opc = 2) Then
            arityOk = True
          End If
        End If
      End If
      If Not arityOk Then
        Printf(MSG_TA_OP_ARITY)
        ta_errCount = ta_errCount + 1
      End If
      If Not hasStructParam Then
        Printf(MSG_TA_OP_STRUCT)
        ta_errCount = ta_errCount + 1
      End If
      Dim opRetTy As TypeInfo = TaResolveTypeEx(g_nodes(d).tyKind, g_nodes(d).tyInnerKind, 0, g_nodes(d).tyNamePos, g_nodes(d).tyNameLen)
      If opRetTy.kind = TyNothing Then
        Printf(MSG_TA_OP_RET)
        ta_errCount = ta_errCount + 1
      End If
      Dim opPriv As Integer = 0
      If g_nodes(d).op = 2 Then
        opPriv = 1
      End If
      If TaHasDupOp(opTok, opPriv, g_nodes(d).modId, ops, opc) Then
        Printf(MSG_TA_OP_DUP)
        ta_errCount = ta_errCount + 1
      End If
      ta_ops(ta_opCount) = TaOpInfo With (
        opTok = opTok,
        isPrivate = opPriv,
        modId = g_nodes(d).modId,
        paramStart = ops,
        paramCount = opc,
        retTy = opRetTy
      )
      ta_opCount = ta_opCount + 1
    End If
  End For

  ' Pass 2: Validate Global initializers, ExternDecl types, and Function bodies
  Dim gi As Integer = 0
  Dim fi As Integer = 0
  Dim oi2 As Integer = 0
  For i = 0 To defCount - 1 Do
    Dim d As Integer = g_kids(defStart + i)
    Dim dk As Integer = g_nodes(d).kind
    ta_curModId = g_nodes(d).modId
    ta_altModId = g_nodes(d).third
    If dk = NdGlobalVar Then
      Dim gty As TypeInfo = ta_globals(gi).ty
      TaValidateType(gty)
      Dim initId As Integer = ta_globals(gi).initNode
      Dim atNode As Integer = g_nodes(d).right
      If atNode >= 0 Then
        If initId >= 0 Then
          Printf(MSG_TA_AT_WITH_INIT)
          ta_errCount = ta_errCount + 1
        End If
        If g_nodes(atNode).kind <> NdIntLit Then
          Printf(MSG_TA_AT_CONST_ADDR)
          ta_errCount = ta_errCount + 1
        End If
      End If
      If initId >= 0 Then
        If (gty.arrLen > 0) And (g_nodes(initId).kind = NdArrayLit) Then
          If g_nodes(initId).childCount > gty.arrLen Then
            Printf("Semantic Type Error: Static array initializer has %d elements, exceeding size %d\n", g_nodes(initId).childCount, gty.arrLen)
            ta_errCount = ta_errCount + 1
          Else
            Dim elemTy As TypeInfo With ( kind = gty.kind, typeIdx = gty.typeIdx, arrLen = 0, namePos = gty.namePos, nameLen = gty.nameLen, innerKind = gty.innerKind )
            For ei = 0 To g_nodes(initId).childCount - 1 Do
              Dim el As Integer = g_kids(g_nodes(initId).firstChild + ei)
              Dim elTy As TypeInfo = TaTypeOfExpr(el)
              If Not TaIsAssignable(elemTy, elTy, el) Then
                Printf("Semantic Type Error: Static array element type mismatch\n")
                ta_errCount = ta_errCount + 1
              End If
            End For
          End If
        Else
          Dim actualTy As TypeInfo = TaTypeOfExpr(initId)
          If Not TaIsAssignable(gty, actualTy, initId) Then
            Printf("Semantic Type Error: Static global initializer type mismatch\n")
            ta_errCount = ta_errCount + 1
          End If
        End If
        If Not TaIsConstExpr(gty, initId) Then
          Printf("Semantic Type Error: Static variable initializer must be a compile-time constant\n")
          ta_errCount = ta_errCount + 1
        End If
      End If
      gi = gi + 1
    End If
    If dk = NdExternDecl Then
      TaValidateType(ta_funcs(fi).retTy)
      Dim j As Integer = 0
      While j < ta_funcs(fi).paramCount Do
        TaValidateType(ta_params(ta_funcs(fi).paramStart + j).ty)
        j = j + 1
      End While
      fi = fi + 1
    End If
    If dk = NdFuncDef Then
      ta_curRetTy = ta_funcs(fi).bodyRetTy
      TaValidateType(ta_curRetTy)
      ta_localCount = 0
      ta_labelCount = 0
      ta_loopDepth = 0
      ta_deferDepth = 0
      ta_curFrameBytes = 0
      ta_curFrameReported = 0
      Dim j As Integer = 0
      While j < g_nodes(d).extraCount Do
        Dim pn As Integer = g_kids(g_nodes(d).extraStart + j)
        Dim pInfo As TaParamInfo = ta_params(ta_funcs(fi).paramStart + j)
        Dim pty As TypeInfo = pInfo.ty
        TaValidateType(pty)
        If (pInfo.isByRef = 0) And (pty.arrLen >= 0) Then
          TaAccountStackLocal(g_nodes(pn).intVal, g_nodes(pn).lenVal, pty)
        End If
        ta_locals(ta_localCount) = TaLocalSym With ( namePos = g_nodes(pn).intVal, nameLen = g_nodes(pn).lenVal, ty = pty )
        ta_localCount = ta_localCount + 1
        j = j + 1
      End While
      TaResetScopes()
      TaCollectLabels(g_nodes(d).firstChild, g_nodes(d).childCount)
      TaResetScopes()
      j = 0
      While j < g_nodes(d).childCount Do
        TaCheckStmt(g_kids(g_nodes(d).firstChild + j))
        j = j + 1
      End While
      TaCheckFrameBudget("Function", ta_funcs(fi).namePos, ta_funcs(fi).nameLen)
      fi = fi + 1
    End If
    If dk = NdOpDef Then
      ta_curRetTy = ta_ops(oi2).retTy
      TaValidateType(ta_curRetTy)
      ta_localCount = 0
      ta_labelCount = 0
      ta_loopDepth = 0
      ta_deferDepth = 0
      ta_curFrameBytes = 0
      ta_curFrameReported = 0
      For j = 0 To g_nodes(d).extraCount - 1 Do
        Dim pn As Integer = g_kids(g_nodes(d).extraStart + j)
        Dim pInfo As TaParamInfo = ta_params(ta_ops(oi2).paramStart + j)
        Dim pty As TypeInfo = pInfo.ty
        TaValidateType(pty)
        If (pInfo.isByRef = 0) And (pty.arrLen >= 0) Then
          TaAccountStackLocal(g_nodes(pn).intVal, g_nodes(pn).lenVal, pty)
        End If
        ta_locals(ta_localCount) = TaLocalSym With ( namePos = g_nodes(pn).intVal, nameLen = g_nodes(pn).lenVal, ty = pty )
        ta_localCount = ta_localCount + 1
      End For
      TaResetScopes()
      TaCollectLabels(g_nodes(d).firstChild, g_nodes(d).childCount)
      TaResetScopes()
      For j = 0 To g_nodes(d).childCount - 1 Do
        TaCheckStmt(g_kids(g_nodes(d).firstChild + j))
      End For
      TaCheckFrameBudget("Operator", 0, 0)
      oi2 = oi2 + 1
    End If
    If dk = NdTestDef Then
      ta_curRetTy = TaMakePrim(TyNothing)
      ta_localCount = 0
      ta_labelCount = 0
      ta_loopDepth = 0
      ta_deferDepth = 0
      ta_curFrameBytes = 0
      ta_curFrameReported = 0
      TaResetScopes()
      TaCollectLabels(g_nodes(d).firstChild, g_nodes(d).childCount)
      TaResetScopes()
      For j = 0 To g_nodes(d).childCount - 1 Do
        TaCheckStmt(g_kids(g_nodes(d).firstChild + j))
      End For
      TaCheckFrameBudget("Test", 0, 0)
    End If
  End For

  Return ta_errCount
End Function
