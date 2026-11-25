' VerBose Self-Hosting Compiler (verbose) - Complete Typed LLVM IR Code Generator

Include "constants.vb"
Include "parser.vb"

Declare Function fprintf(fp As Pointer, fmt As Pointer, ...) As Integer
Declare Function fputc(c As Integer, fp As Pointer) As Integer
Declare Function fputs(s As Pointer, fp As Pointer) As Integer
Declare Function strtod(s As Pointer, endp As Pointer) As Double
Declare Function memcpy(ByRef dst As Long, ByRef src As Double, n As Long) As Pointer

Structure CgEnumInfo
  Dim namePos, nameLen, memStart, memCount, isPrivate, modId As Integer
End Structure

Structure CgEnumMem
  Dim namePos, nameLen, value As Integer
End Structure

Structure CgStructInfo
  Dim namePos, nameLen, fieldStart, fieldCount As Integer
End Structure

Structure CgFieldInfo
  Dim namePos, nameLen As Integer, ty As TypeInfo
End Structure

Structure CgGlobalInfo
  Dim namePos, nameLen, isPublic, isPrivate, modId, initNode, isVolatile, hasFixedAddr, fixedAddr As Integer, ty As TypeInfo
End Structure

Structure CgParamInfo
  Dim isByRef, namePos, nameLen As Integer, ty As TypeInfo
End Structure

Structure CgFuncInfo
  Dim namePos, nameLen, isExtern, isVariadic, isPublic, isPrivate, isAsync, modId, paramStart, paramCount As Integer
  Dim retTy, bodyRetTy As TypeInfo
End Structure

Structure CgOpInfo
  Dim opTok, isPublic, isPrivate, modId, paramStart, paramCount As Integer
  Dim retTy As TypeInfo
End Structure

Structure CgLocalSym
  Dim namePos, nameLen, slotReg, isIndirect, staticPlusOne, isVolatile As Integer, ty As TypeInfo
End Structure

Structure CgStaticLocal
  Dim ty As TypeInfo, initNode, isVolatile As Integer
End Structure

Structure CgStrConst
  Dim srcPos, srcLen, byteLen As Integer
End Structure

Private Static cg_out As Pointer, cg_curRetTy As TypeInfo
Private Static cg_reg = 1, cg_lbl = 1, cg_usesArena = 0, cg_usesAssert = 0, cg_usesStrConcat = 0, cg_usesStrInterp = 0, cg_usesStrEq = 0, cg_usesAsync = 0, cg_blockEnded = 0, cg_testMode = 0, cg_inTestFn = 0, cg_testCount = 0, cg_curModId = 0 As Integer
Private Static cg_target = 0, cg_ptrSize = 8, cg_ptrAlign = 8, cg_isFreestanding = 0 As Integer
Private Static cg_usesPrintf = 0, cg_usesMalloc = 0, cg_usesCalloc = 0, cg_usesRealloc = 0, cg_usesFree = 0, cg_usesFopen = 0, cg_usesGetc = 0, cg_usesPutc = 0 As Integer

Public Sub CgSetTestMode(enabled As Integer)
  cg_testMode = enabled
End Sub

Public Function CgUsesAsync() As Integer
  Return cg_usesAsync
End Function

Public Sub CgSetTarget(targetId As Integer)
  cg_target = targetId
  If targetId = TgtArmM Then
    cg_ptrSize = 4
    cg_ptrAlign = 4
    cg_isFreestanding = 1
  Else
    cg_ptrSize = 8
    cg_ptrAlign = 8
    cg_isFreestanding = 0
  End If
End Sub

Private Static cg_enums() As CgEnumInfo, cg_enumMems() As CgEnumMem
Private Static cg_structs() As CgStructInfo, cg_fields() As CgFieldInfo
Private Static cg_globals() As CgGlobalInfo, cg_funcs() As CgFuncInfo
Private Static cg_ops() As CgOpInfo
Private Static cg_params() As CgParamInfo, cg_locals() As CgLocalSym
Private Static cg_strs() As CgStrConst, cg_staticLocals() As CgStaticLocal
Private Static cg_withPtrs(16), cg_withVols(16) As Integer, cg_withTys(16) As TypeInfo
Private Static cg_loopBreakLbls(64), cg_loopContLbls(64) As Integer
Private Static cg_enumCount, cg_enumMemCount, cg_structCount, cg_fieldCount, cg_globalCount, cg_funcCount, cg_opCount, cg_paramCount, cg_localCount, cg_strCount, cg_staticLocalCount, cg_withDepth, cg_loopDepth As Integer = 0

Private Function CgNextReg() As Integer
  Dim r As Integer = cg_reg
  cg_reg = cg_reg + 1
  Return r
End Function

Private Function CgNextLbl() As Integer
  Dim l As Integer = cg_lbl
  cg_lbl = cg_lbl + 1
  Return l
End Function

Private Sub CgEmitSlice(pos, slen As Integer)
  For i = 0 To slen - 1 Do
    Dim ch As Integer = g_srcPool(pos + i)
    fputc(ch, cg_out)
  End For
End Sub

Private Sub CgEmitSymName(pos, slen, modId, isPrivate As Integer)
  If (isPrivate = 1) And (modId > 0) Then
    fprintf(cg_out, "__mod%d_", modId)
  Else
    If (cg_target = TgtWindowsX64) And (isPrivate = 0) Then
      If SliceEqualsStr(pos, slen, "popen", 5) Then
        fprintf(cg_out, "_popen")
        Return
      End If
      If SliceEqualsStr(pos, slen, "pclose", 6) Then
        fprintf(cg_out, "_pclose")
        Return
      End If
    End If
  End If
  CgEmitSlice(pos, slen)
End Sub

Private Function CgFindEnum(pos, slen As Integer) As Integer
  For i = 0 To cg_enumCount - 1 Do
    If SlicesEqual(cg_enums(i).namePos, cg_enums(i).nameLen, pos, slen) Then
      Return i
    End If
  End For
  Return -1
End Function

Private Function CgFindEnumVal(enumIdx, memPos, memLen As Integer, ByRef outVal As Integer) As Boolean
  Dim st = cg_enums(enumIdx).memStart, cnt = cg_enums(enumIdx).memCount, i = 0 As Integer
  While i < cnt Do
    If SlicesEqual(cg_enumMems(st + i).namePos, cg_enumMems(st + i).nameLen, memPos, memLen) Then
      outVal = cg_enumMems(st + i).value
      Return True
    End If
    i = i + 1
  End While
  Return False
End Function

Private Function CgFindUnqualEnumVal(memPos, memLen As Integer, ByRef outVal As Integer) As Boolean
  For i = 0 To cg_enumCount - 1 Do
    If (cg_enums(i).isPrivate = 1) And (cg_enums(i).modId = cg_curModId) Then
      If CgFindEnumVal(i, memPos, memLen, ByRef outVal) Then
        Return True
      End If
    End If
  End For
  For i = 0 To cg_enumCount - 1 Do
    If cg_enums(i).isPrivate = 0 Then
      If CgFindEnumVal(i, memPos, memLen, ByRef outVal) Then
        Return True
      End If
    End If
  End For
  Return False
End Function

Private Function CgFindStruct(pos, slen As Integer) As Integer
  For i = 0 To cg_structCount - 1 Do
    If SlicesEqual(cg_structs(i).namePos, cg_structs(i).nameLen, pos, slen) Then
      Return i
    End If
  End For
  Return -1
End Function

Private Function CgFindStructField(structIdx, fPos, fLen As Integer, ByRef outTy As TypeInfo) As Integer
  Dim st = cg_structs(structIdx).fieldStart, cnt = cg_structs(structIdx).fieldCount, i = 0 As Integer
  While i < cnt Do
    If SlicesEqual(cg_fields(st + i).namePos, cg_fields(st + i).nameLen, fPos, fLen) Then
      outTy = cg_fields(st + i).ty
      Return i
    End If
    i = i + 1
  End While
  Return -1
End Function

Private Function CgFindGlobal(pos, slen As Integer) As Integer
  For i = 0 To cg_globalCount - 1 Do
    If (cg_globals(i).isPrivate = 1) And (cg_globals(i).modId = cg_curModId) And SlicesEqual(cg_globals(i).namePos, cg_globals(i).nameLen, pos, slen) Then
      Return i
    End If
  End For
  For i = 0 To cg_globalCount - 1 Do
    If (cg_globals(i).isPrivate = 0) And SlicesEqual(cg_globals(i).namePos, cg_globals(i).nameLen, pos, slen) Then
      Return i
    End If
  End For
  Return -1
End Function

Private Function CgFindFunc(pos, slen As Integer) As Integer
  For i = 0 To cg_funcCount - 1 Do
    If (cg_funcs(i).isPrivate = 1) And (cg_funcs(i).modId = cg_curModId) And SlicesEqual(cg_funcs(i).namePos, cg_funcs(i).nameLen, pos, slen) Then
      Return i
    End If
  End For
  For i = 0 To cg_funcCount - 1 Do
    If (cg_funcs(i).isPrivate = 0) And SlicesEqual(cg_funcs(i).namePos, cg_funcs(i).nameLen, pos, slen) Then
      Return i
    End If
  End For
  Return -1
End Function

Private Function CgFindLocal(pos, slen As Integer) As Integer
  Dim i As Integer = cg_localCount - 1
  While i >= 0 Do
    If SlicesEqual(cg_locals(i).namePos, cg_locals(i).nameLen, pos, slen) Then
      Return i
    End If
    i = i - 1
  End While
  Return -1
End Function

Private Function CgNormKind(k As Integer) As Integer
  If k = TyEnum Then
    Return TyInt
  End If
  Return k
End Function

Private Function CgTypesMatch(t1 As TypeInfo, t2 As TypeInfo) As Boolean
  If t1.arrLen <> t2.arrLen Then
    Return False
  End If
  Dim k1 As Integer = CgNormKind(t1.kind)
  Dim k2 As Integer = CgNormKind(t2.kind)
  If k1 <> k2 Then
    Return False
  End If
  If k1 = TyStruct Then
    Return t1.typeIdx = t2.typeIdx
  End If
  Return True
End Function

Private Function CgIsIntLitNode(id As Integer) As Boolean
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

Private Function CgIsFloatLitNode(id As Integer) As Boolean
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

Private Function CgOpParamMatchesLit(paramTy As TypeInfo, argTy As TypeInfo, argNode As Integer) As Boolean
  If CgTypesMatch(paramTy, argTy) Then
    Return True
  End If
  If (paramTy.arrLen = 0) And ((paramTy.kind = TySingle) Or (paramTy.kind = TyDouble)) And CgIsFloatLitNode(argNode) Then
    Return True
  End If
  Dim pk As Integer = CgNormKind(paramTy.kind)
  If (paramTy.arrLen = 0) And ((pk = TyByte) Or (pk = TyShort) Or (pk = TyInt) Or (pk = TyLong)) And CgIsIntLitNode(argNode) Then
    Return True
  End If
  Return False
End Function

Private Function CgFindUnaryOp(opTok As Integer, t As TypeInfo) As Integer
  For i = 0 To cg_opCount - 1 Do
    If (cg_ops(i).isPrivate = 1) And (cg_ops(i).modId = cg_curModId) And (cg_ops(i).opTok = opTok) And (cg_ops(i).paramCount = 1) Then
      If CgTypesMatch(cg_params(cg_ops(i).paramStart).ty, t) Then
        Return i
      End If
    End If
  End For
  For i = 0 To cg_opCount - 1 Do
    If (cg_ops(i).isPrivate = 0) And (cg_ops(i).opTok = opTok) And (cg_ops(i).paramCount = 1) Then
      If CgTypesMatch(cg_params(cg_ops(i).paramStart).ty, t) Then
        Return i
      End If
    End If
  End For
  Return -1
End Function

Private Function CgFindBinOp(opTok As Integer, t1 As TypeInfo, e1 As Integer, t2 As TypeInfo, e2 As Integer) As Integer
  For i = 0 To cg_opCount - 1 Do
    If (cg_ops(i).isPrivate = 1) And (cg_ops(i).modId = cg_curModId) And (cg_ops(i).opTok = opTok) And (cg_ops(i).paramCount = 2) Then
      Dim p0 As TypeInfo = cg_params(cg_ops(i).paramStart).ty
      Dim p1 As TypeInfo = cg_params(cg_ops(i).paramStart + 1).ty
      If CgTypesMatch(p0, t1) And CgTypesMatch(p1, t2) Then
        Return i
      End If
    End If
  End For
  For i = 0 To cg_opCount - 1 Do
    If (cg_ops(i).isPrivate = 0) And (cg_ops(i).opTok = opTok) And (cg_ops(i).paramCount = 2) Then
      Dim p0 As TypeInfo = cg_params(cg_ops(i).paramStart).ty
      Dim p1 As TypeInfo = cg_params(cg_ops(i).paramStart + 1).ty
      If CgTypesMatch(p0, t1) And CgTypesMatch(p1, t2) Then
        Return i
      End If
    End If
  End For
  For i = 0 To cg_opCount - 1 Do
    If (cg_ops(i).isPrivate = 1) And (cg_ops(i).modId = cg_curModId) And (cg_ops(i).opTok = opTok) And (cg_ops(i).paramCount = 2) Then
      Dim p0 As TypeInfo = cg_params(cg_ops(i).paramStart).ty
      Dim p1 As TypeInfo = cg_params(cg_ops(i).paramStart + 1).ty
      If CgOpParamMatchesLit(p0, t1, e1) And CgOpParamMatchesLit(p1, t2, e2) Then
        Return i
      End If
    End If
  End For
  For i = 0 To cg_opCount - 1 Do
    If (cg_ops(i).isPrivate = 0) And (cg_ops(i).opTok = opTok) And (cg_ops(i).paramCount = 2) Then
      Dim p0 As TypeInfo = cg_params(cg_ops(i).paramStart).ty
      Dim p1 As TypeInfo = cg_params(cg_ops(i).paramStart + 1).ty
      If CgOpParamMatchesLit(p0, t1, e1) And CgOpParamMatchesLit(p1, t2, e2) Then
        Return i
      End If
    End If
  End For
  Return -1
End Function

Private Function CgHasBoolStructOp(opTok As Integer) As Boolean
  For i = 0 To cg_opCount - 1 Do
    If (cg_ops(i).opTok = opTok) And (cg_ops(i).paramCount = 2) Then
      Dim p0 As TypeInfo = cg_params(cg_ops(i).paramStart).ty
      If (p0.arrLen = 0) And (p0.kind = TyBoolean) Then
        Return True
      End If
    End If
  End For
  Return False
End Function

Private Sub CgEmitOpName(opTok, arity As Integer)
  Match opTok With
    Case TkPlus Then fprintf(cg_out, "add")
    Case TkMinus Then
      If arity = 1 Then
        fprintf(cg_out, "neg")
      Else
        fprintf(cg_out, "sub")
      End If
    Case TkStar Then fprintf(cg_out, "mul")
    Case TkSlash Then fprintf(cg_out, "div")
    Case TkMod Then fprintf(cg_out, "mod")
    Case TkAmp Then fprintf(cg_out, "amp")
    Case TkEq Then fprintf(cg_out, "eq")
    Case TkNeq Then fprintf(cg_out, "neq")
    Case TkLt Then fprintf(cg_out, "lt")
    Case TkGt Then fprintf(cg_out, "gt")
    Case TkLe Then fprintf(cg_out, "le")
    Case TkGe Then fprintf(cg_out, "ge")
    Case TkAnd Then fprintf(cg_out, "and")
    Case TkOr Then fprintf(cg_out, "or")
    Case TkXor Then fprintf(cg_out, "xor")
    Case TkShl Then fprintf(cg_out, "shl")
    Case TkShr Then fprintf(cg_out, "shr")
    Case TkNot Then fprintf(cg_out, "not")
    Case _ Then fprintf(cg_out, "op%d", opTok)
  End Match
End Sub

Private Sub CgEmitTypeTag(ty As TypeInfo)
  Match ty.kind With
    Case TyInt, TyEnum Then fprintf(cg_out, "i32")
    Case TyByte Then fprintf(cg_out, "i8")
    Case TyShort Then fprintf(cg_out, "i16")
    Case TyLong Then fprintf(cg_out, "i64")
    Case TySingle Then fprintf(cg_out, "f32")
    Case TyDouble Then fprintf(cg_out, "f64")
    Case TyBoolean Then fprintf(cg_out, "bool")
    Case TyPointer, TyTask Then fprintf(cg_out, "ptr")
    Case TyArena Then fprintf(cg_out, "arena")
    Case TyStruct Then
      If ty.typeIdx >= 0 Then
        CgEmitSlice(cg_structs(ty.typeIdx).namePos, cg_structs(ty.typeIdx).nameLen)
      Else
        CgEmitSlice(ty.namePos, ty.nameLen)
      End If
    Case _ Then fprintf(cg_out, "unk")
  End Match
End Sub

Private Sub CgEmitOpSymName(opIdx As Integer)
  Dim oi As CgOpInfo = cg_ops(opIdx)
  If (oi.isPrivate = 1) And (oi.modId > 0) Then
    fprintf(cg_out, "__mod%d_", oi.modId)
  End If
  fprintf(cg_out, "__vb_op_")
  CgEmitOpName(oi.opTok, oi.paramCount)
  For pi = 0 To oi.paramCount - 1 Do
    fprintf(cg_out, "_")
    CgEmitTypeTag(cg_params(oi.paramStart + pi).ty)
  End For
End Sub

Private Function CgResolveTypeEx(rawKind, rawInnerKind, arrLen, nPos, nLen As Integer) As TypeInfo
  Dim res As TypeInfo With (
    kind = rawKind,
    typeIdx = -1,
    arrLen = arrLen,
    namePos = nPos,
    nameLen = nLen,
    innerKind = rawInnerKind
  )
  If rawKind = TyArena Then
    cg_usesArena = 1
  End If
  If rawKind = TyStruct Then
    Dim eIdx As Integer = CgFindEnum(nPos, nLen)
    If eIdx >= 0 Then
      res.kind = TyEnum
      res.typeIdx = eIdx
    Else
      Dim sIdx As Integer = CgFindStruct(nPos, nLen)
      res.typeIdx = sIdx
    End If
  Else
    If (rawKind = TyTask) And (rawInnerKind = TyStruct) Then
      Dim eIdx As Integer = CgFindEnum(nPos, nLen)
      If eIdx >= 0 Then
        res.innerKind = TyEnum
        res.typeIdx = eIdx
      Else
        Dim sIdx As Integer = CgFindStruct(nPos, nLen)
        res.typeIdx = sIdx
      End If
    End If
  End If
  Return res
End Function

Private Function CgResolveType(rawKind, arrLen, nPos, nLen As Integer) As TypeInfo
  Return CgResolveTypeEx(rawKind, TyNothing, arrLen, nPos, nLen)
End Function

Private Function CgIsAggregate(ty As TypeInfo) As Boolean
  If ty.arrLen > 0 Then
    Return True
  End If
  If ty.arrLen < 0 Then
    Return False
  End If
  If (ty.kind = TyArena) Or (ty.kind = TyStruct) Then
    Return True
  End If
  Return False
End Function

Private Function CgElemType(ty As TypeInfo) As TypeInfo
  Dim e As TypeInfo With (
    kind = ty.kind,
    typeIdx = ty.typeIdx,
    arrLen = 0,
    namePos = ty.namePos,
    nameLen = ty.nameLen,
    innerKind = ty.innerKind
  )
  Return e
End Function

Private Function CgBaseAlign(kind As Integer, typeIdx As Integer) As Integer
  Match kind With
    Case TyByte, TyBoolean, TyNothing Then Return 1
    Case TyShort Then Return 2
    Case TyInt, TyEnum, TySingle Then Return 4
    Case TyPointer, TyTask Then Return cg_ptrAlign
    Case TyLong, TyArena, TyDouble Then Return 8
    Case TyStruct Then
      If typeIdx < 0 Then
        Return 8
      End If
      Dim st As Integer = cg_structs(typeIdx).fieldStart
      Dim cnt As Integer = cg_structs(typeIdx).fieldCount
      Dim mx As Integer = 1
      For i = 0 To cnt - 1 Do
        Dim fa As Integer = CgTypeAlign(cg_fields(st + i).ty)
        If fa > mx Then
          mx = fa
        End If
      End For
      Return mx
    Case _ Then Return 8
  End Match
End Function

Private Function CgTypeAlign(ty As TypeInfo) As Integer
  If ty.arrLen < 0 Then
    Return cg_ptrAlign
  End If
  Return CgBaseAlign(ty.kind, ty.typeIdx)
End Function

Private Function CgAlignUp(off As Integer, a As Integer) As Integer
  If a <= 1 Then
    Return off
  End If
  Return ((off + a - 1) / a) * a
End Function

Private Function CgStructSize(structIdx As Integer) As Integer
  If structIdx < 0 Then
    Return 16
  End If
  Dim st As Integer = cg_structs(structIdx).fieldStart
  Dim cnt As Integer = cg_structs(structIdx).fieldCount
  Dim off As Integer = 0
  Dim mx As Integer = 1
  For i = 0 To cnt - 1 Do
    Dim fty As TypeInfo = cg_fields(st + i).ty
    Dim a As Integer = CgTypeAlign(fty)
    If a > mx Then
      mx = a
    End If
    off = CgAlignUp(off, a) + CgTypeSize(fty)
  End For
  Dim total As Integer = CgAlignUp(off, mx)
  If total = 0 Then
    Return 8
  End If
  Return total
End Function

Private Function CgTypeSize(ty As TypeInfo) As Integer
  If ty.arrLen < 0 Then
    Return cg_ptrSize
  End If
  Dim elemSz As Integer = 0
  Match ty.kind With
    Case TyByte, TyBoolean Then elemSz = 1
    Case TyShort Then elemSz = 2
    Case TyInt, TyEnum, TySingle Then elemSz = 4
    Case TyPointer, TyTask Then elemSz = cg_ptrSize
    Case TyLong, TyDouble Then elemSz = 8
    Case TyArena Then elemSz = 24
    Case TyStruct Then elemSz = CgStructSize(ty.typeIdx)
    Case _ Then elemSz = 0
  End Match
  If ty.arrLen > 0 Then
    Return ty.arrLen * elemSz
  End If
  Return elemSz
End Function

Private Sub CgEmitBaseLlvmType(kind As Integer, typeIdx As Integer, nPos As Integer, nLen As Integer)
  Match kind With
    Case TyNothing Then fprintf(cg_out, "void")
    Case TyInt, TyEnum Then fprintf(cg_out, "i32")
    Case TyByte Then fprintf(cg_out, "i8")
    Case TyShort Then fprintf(cg_out, "i16")
    Case TyLong Then fprintf(cg_out, "i64")
    Case TySingle Then fprintf(cg_out, "float")
    Case TyDouble Then fprintf(cg_out, "double")
    Case TyBoolean Then fprintf(cg_out, "i1")
    Case TyPointer, TyTask Then fprintf(cg_out, "ptr")
    Case TyArena Then
      cg_usesArena = 1
      fprintf(cg_out, "%%struct.__vb_arena")
    Case TyStruct Then
      fprintf(cg_out, "%%struct.")
      If typeIdx >= 0 Then
        CgEmitSlice(cg_structs(typeIdx).namePos, cg_structs(typeIdx).nameLen)
      Else
        CgEmitSlice(nPos, nLen)
      End If
    Case _ Then fprintf(cg_out, "i32")
  End Match
End Sub

Private Sub CgEmitLlvmType(ty As TypeInfo)
  If ty.arrLen < 0 Then
    fprintf(cg_out, "ptr")
    Return
  End If
  If ty.arrLen > 0 Then
    fprintf(cg_out, "[%d x ", ty.arrLen)
    CgEmitBaseLlvmType(ty.kind, ty.typeIdx, ty.namePos, ty.nameLen)
    fprintf(cg_out, "]")
    Return
  End If
  CgEmitBaseLlvmType(ty.kind, ty.typeIdx, ty.namePos, ty.nameLen)
End Sub

Private Sub CgEmitParamLlvmType(p As CgParamInfo)
  If (p.isByRef = 1) Or (p.ty.arrLen <> 0) Or (p.ty.kind = TyStruct) Or (p.ty.kind = TyArena) Then
    fprintf(cg_out, "ptr")
  Else
    CgEmitLlvmType(p.ty)
  End If
End Sub

' SysV x86-64 C ABI classification for ByVal C structs:
' type codes: 1=i8, 2=i16, 3=i32, 4=i64, 5=float, 6=double, 7=<2 x float>, 8=ptr byval(%struct.X)
Private Sub CgFlattenStruct(baseOff As Integer, ty As TypeInfo, leafOffs() As Integer, leafKinds() As Integer, leafSizes() As Integer, ByRef leafCount As Integer)
  If ty.arrLen > 0 Then
    Dim elemTy As TypeInfo = CgElemType(ty)
    Dim esz As Integer = CgTypeSize(elemTy)
    Dim i As Integer = 0
    While i < ty.arrLen Do
      CgFlattenStruct(baseOff + i * esz, elemTy, leafOffs, leafKinds, leafSizes, ByRef leafCount)
      i = i + 1
    End While
    Return
  End If
  If (ty.kind = TyStruct) And (ty.typeIdx >= 0) Then
    Dim st As Integer = cg_structs(ty.typeIdx).fieldStart
    Dim cnt As Integer = cg_structs(ty.typeIdx).fieldCount
    Dim off As Integer = baseOff
    For i = 0 To cnt - 1 Do
      Dim fty As TypeInfo = cg_fields(st + i).ty
      Dim a As Integer = CgTypeAlign(fty)
      off = CgAlignUp(off, a)
      CgFlattenStruct(off, fty, leafOffs, leafKinds, leafSizes, ByRef leafCount)
      off = off + CgTypeSize(fty)
    End For
    Return
  End If
  If leafCount < 64 Then
    leafOffs(leafCount) = baseOff
    leafKinds(leafCount) = ty.kind
    Dim sz As Integer = CgTypeSize(ty)
    If sz < 1 Then
      sz = 1
    End If
    leafSizes(leafCount) = sz
    leafCount = leafCount + 1
  End If
End Sub

Private Function CgClassifyChunk(startOff As Integer, chunkBytes As Integer, leafOffs() As Integer, leafKinds() As Integer, leafSizes() As Integer, leafCount As Integer) As Integer
  Dim inCnt As Integer = 0
  Dim allSse As Boolean = True
  Dim singleCnt As Integer = 0
  Dim doubleCnt As Integer = 0
  For i = 0 To leafCount - 1 Do
    Dim o As Integer = leafOffs(i)
    Dim sz As Integer = leafSizes(i)
    If (o < startOff + 8) And (o + sz > startOff) Then
      inCnt = inCnt + 1
      If leafKinds(i) = TySingle Then
        singleCnt = singleCnt + 1
      Else
        If leafKinds(i) = TyDouble Then
          doubleCnt = doubleCnt + 1
        Else
          allSse = False
        End If
      End If
    End If
  End For
  If (inCnt > 0) And allSse Then
    If (inCnt = 1) And (singleCnt = 1) Then
      Return 5
    End If
    If (inCnt = 1) And (doubleCnt = 1) Then
      Return 6
    End If
    If (inCnt = 2) And (singleCnt = 2) Then
      Return 7
    End If
    If chunkBytes <= 4 Then
      Return 5
    End If
    Return 7
  End If
  If chunkBytes <= 1 Then
    Return 1
  End If
  If chunkBytes <= 2 Then
    Return 2
  End If
  If chunkBytes <= 4 Then
    Return 3
  End If
  Return 4
End Function

' Returns mode:
'   0 = ptr byval(%struct.X) (code 8)
'   1 = 1 ABI register chunk (outT0)
'   2 = 2 ABI register chunks (outT0, outT1)
'   3 = direct %struct.X value (HFA on ARM64, small struct on ARM-M)
'   4 = indirect plain ptr to caller stack copy (code 9: >16B on ARM64, non-1/2/4/8B on Win64)
Private Function CgClassifyCStruct(structIdx As Integer, ByRef outT0 As Integer, ByRef outT1 As Integer) As Integer
  outT0 = 0
  outT1 = 0
  Dim totalSz As Integer = CgStructSize(structIdx)
  If cg_target = TgtWindowsX64 Then
    Match totalSz With
      Case 1 Then
        outT0 = 1
        Return 1
      Case 2 Then
        outT0 = 2
        Return 1
      Case 4 Then
        outT0 = 3
        Return 1
      Case 8 Then
        outT0 = 4
        Return 1
      Case _ Then Return 4
    End Match
  End If
  If cg_target = TgtArmM Then
    If totalSz > 16 Then
      Return 0
    End If
    Return 3
  End If
  If (cg_target = TgtLinuxArm64) Or (cg_target = TgtMacosArm64) Then
    Dim aOffs(64), aKinds(64), aSizes(64) As Integer
    Dim aCnt As Integer = 0
    Dim aTy As TypeInfo With (
      kind = TyStruct,
      typeIdx = structIdx,
      arrLen = 0,
      namePos = cg_structs(structIdx).namePos,
      nameLen = cg_structs(structIdx).nameLen
    )
    CgFlattenStruct(0, aTy, aOffs, aKinds, aSizes, ByRef aCnt)
    If (aCnt >= 1) And (aCnt <= 4) Then
      Dim k0 As Integer = aKinds(0)
      If (k0 = TySingle) Or (k0 = TyDouble) Then
        Dim isHfa As Boolean = True
        For hi = 1 To aCnt - 1 Do
          If aKinds(hi) <> k0 Then
            isHfa = False
          End If
        End For
        If isHfa Then
          Return 3
        End If
      End If
    End If
    If totalSz > 16 Then
      Return 4
    End If
    If totalSz <= 8 Then
      If totalSz <= 1 Then
        outT0 = 1
      Else
        If totalSz <= 2 Then
          outT0 = 2
        Else
          If totalSz <= 4 Then
            outT0 = 3
          Else
            outT0 = 4
          End If
        End If
      End If
      Return 1
    End If
    outT0 = 4
    outT1 = 4
    Return 2
  End If
  If totalSz > 16 Then
    Return 0
  End If
  Dim leafOffs(64) As Integer
  Dim leafKinds(64) As Integer
  Dim leafSizes(64) As Integer
  Dim leafCount As Integer = 0
  Dim sTy As TypeInfo With (
    kind = TyStruct,
    typeIdx = structIdx,
    arrLen = 0,
    namePos = cg_structs(structIdx).namePos,
    nameLen = cg_structs(structIdx).nameLen
  )
  CgFlattenStruct(0, sTy, leafOffs, leafKinds, leafSizes, ByRef leafCount)
  If totalSz <= 8 Then
    outT0 = CgClassifyChunk(0, totalSz, leafOffs, leafKinds, leafSizes, leafCount)
    Return 1
  End If
  outT0 = CgClassifyChunk(0, 8, leafOffs, leafKinds, leafSizes, leafCount)
  outT1 = CgClassifyChunk(8, totalSz - 8, leafOffs, leafKinds, leafSizes, leafCount)
  Return 2
End Function

Private Sub CgEmitCAbiCode(code As Integer, structIdx As Integer)
  Match code With
    Case 1 Then fprintf(cg_out, "i8")
    Case 2 Then fprintf(cg_out, "i16")
    Case 3 Then fprintf(cg_out, "i32")
    Case 4 Then fprintf(cg_out, "i64")
    Case 5 Then fprintf(cg_out, "float")
    Case 6 Then fprintf(cg_out, "double")
    Case 7 Then fprintf(cg_out, "<2 x float>")
    Case 8 Then
      fprintf(cg_out, "ptr byval(%%struct.")
      CgEmitSlice(cg_structs(structIdx).namePos, cg_structs(structIdx).nameLen)
      fprintf(cg_out, ")")
    Case 9 Then fprintf(cg_out, "ptr")
  End Match
End Sub

Private Sub CgEmitExternRetType(retTy As TypeInfo)
  If (retTy.kind = TyStruct) And (retTy.arrLen = 0) And (retTy.typeIdx >= 0) Then
    Dim t0 As Integer = 0
    Dim t1 As Integer = 0
    Dim mode As Integer = CgClassifyCStruct(retTy.typeIdx, ByRef t0, ByRef t1)
    If mode = 1 Then
      CgEmitCAbiCode(t0, retTy.typeIdx)
      Return
    End If
    If mode = 2 Then
      fprintf(cg_out, "{ ")
      CgEmitCAbiCode(t0, retTy.typeIdx)
      fprintf(cg_out, ", ")
      CgEmitCAbiCode(t1, retTy.typeIdx)
      fprintf(cg_out, " }")
      Return
    End If
  End If
  CgEmitLlvmType(retTy)
End Sub

Private Sub CgEmitExternParamType(p As CgParamInfo)
  If (p.isByRef = 0) And (p.ty.kind = TyStruct) And (p.ty.arrLen = 0) And (p.ty.typeIdx >= 0) Then
    Dim t0 As Integer = 0
    Dim t1 As Integer = 0
    Dim mode As Integer = CgClassifyCStruct(p.ty.typeIdx, ByRef t0, ByRef t1)
    If mode = 1 Then
      CgEmitCAbiCode(t0, p.ty.typeIdx)
      Return
    End If
    If mode = 2 Then
      CgEmitCAbiCode(t0, p.ty.typeIdx)
      fprintf(cg_out, ", ")
      CgEmitCAbiCode(t1, p.ty.typeIdx)
      Return
    End If
    If mode = 3 Then
      CgEmitLlvmType(p.ty)
      Return
    End If
    If mode = 4 Then
      CgEmitCAbiCode(9, p.ty.typeIdx)
      Return
    End If
    CgEmitCAbiCode(8, p.ty.typeIdx)
    Return
  End If
  CgEmitParamLlvmType(p)
End Sub

Private Sub CgEmitZeroVal(ty As TypeInfo)
  If ty.arrLen < 0 Then
    fprintf(cg_out, "null")
    Return
  End If
  If ty.arrLen > 0 Then
    fprintf(cg_out, "zeroinitializer")
    Return
  End If
  Match ty.kind With
    Case TyInt, TyByte, TyShort, TyLong, TyEnum Then fprintf(cg_out, "0")
    Case TySingle, TyDouble Then fprintf(cg_out, "0.0")
    Case TyBoolean Then fprintf(cg_out, "false")
    Case TyPointer, TyTask Then fprintf(cg_out, "null")
    Case TyArena, TyStruct Then fprintf(cg_out, "zeroinitializer")
    Case _ Then fprintf(cg_out, "0")
  End Match
End Sub

Private Function CgRegisterString(srcPos As Integer, srcLen As Integer) As Integer
  For k = 0 To cg_strCount - 1 Do
    If SlicesEqual(cg_strs(k).srcPos, cg_strs(k).srcLen, srcPos, srcLen) Then
      Return k
    End If
  End For
  Dim idx As Integer = cg_strCount
  Dim i As Integer = 0
  Dim bytes As Integer = 0
  While i < srcLen Do
    Dim ch As Integer = g_srcPool(srcPos + i)
    If (ch = 92) And ((i + 1) < srcLen) Then
      bytes = bytes + 1
      i = i + 2
    Else
      bytes = bytes + 1
      i = i + 1
    End If
  End While
  cg_strs(idx) = CgStrConst With (
    srcPos = srcPos,
    srcLen = srcLen,
    byteLen = bytes + 1
  )
  cg_strCount = cg_strCount + 1
  Return idx
End Function

Private Sub CgCollectStrings(id As Integer)
  If id < 0 Then
    Return
  End If
  Dim k As Integer = g_nodes(id).kind
  If k = NdTestDef Then
    If cg_testMode = 0 Then
      Return
    End If
    g_nodes(id).op = CgRegisterString(g_nodes(id).intVal, g_nodes(id).lenVal)
    cg_testCount = cg_testCount + 1
    cg_usesPrintf = 1
  End If
  If k = NdAssert Then
    cg_usesAssert = 1
    cg_usesPrintf = 1
  End If
  If k = NdStrLit Then
    Dim sid As Integer = CgRegisterString(g_nodes(id).intVal, g_nodes(id).lenVal)
    g_nodes(id).op = sid
    Return
  End If
  If k = NdStrInterp Then
    cg_usesStrInterp = 1
    cg_usesCalloc = 1
    For ii = 0 To g_nodes(id).childCount - 1 Do
      Dim ch As Integer = g_kids(g_nodes(id).firstChild + ii)
      If g_nodes(ch).kind <> NdStrLit Then
        CgCollectStrings(ch)
      End If
    End For
    Return
  End If
  If k = NdStaticDim Then
    Dim sid As Integer = cg_staticLocalCount
    g_nodes(id).op = sid
    Dim isVol As Integer = 0
    If g_nodes(id).flags = 1 Then
      isVol = 1
    End If
    cg_staticLocals(sid) = CgStaticLocal With (
      ty = CgResolveTypeEx(g_nodes(id).tyKind, g_nodes(id).tyInnerKind, g_nodes(id).tyArrLen, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen),
      initNode = g_nodes(id).left,
      isVolatile = isVol
    )
    cg_staticLocalCount = cg_staticLocalCount + 1
  End If
  If (k = NdFuncDef) And (g_nodes(id).flags = 1) Then
    cg_usesAsync = 1
    cg_usesCalloc = 1
    cg_usesFree = 1
  End If
  If k = NdAwait Then
    cg_usesAsync = 1
    cg_usesFree = 1
  End If
  If k = NdArenaAlloc Then
    cg_usesArena = 1
  End If
  If k = NdHeapAlloc Then
    cg_usesCalloc = 1
  End If
  If k = NdHeapRealloc Then
    cg_usesRealloc = 1
  End If
  If k = NdCall Then
    Dim cnPos As Integer = g_nodes(id).intVal
    Dim cnLen As Integer = g_nodes(id).lenVal
    If SliceEqualsStr(cnPos, cnLen, "ArenaCreate", 11) Then
      cg_usesArena = 1
      cg_usesCalloc = 1
    End If
    If SliceEqualsStr(cnPos, cnLen, "ArenaFromBuffer", 15) Then
      cg_usesArena = 1
    End If
    If SliceEqualsStr(cnPos, cnLen, "ArenaDestroy", 12) Then
      cg_usesArena = 1
      cg_usesFree = 1
    End If
    If SliceEqualsStr(cnPos, cnLen, "Printf", 6) Then
      cg_usesPrintf = 1
    End If
    If SliceEqualsStr(cnPos, cnLen, "Malloc", 6) Then
      cg_usesMalloc = 1
    End If
    If SliceEqualsStr(cnPos, cnLen, "Calloc", 6) Or SliceEqualsStr(cnPos, cnLen, "HeapAllocBytes", 14) Then
      cg_usesCalloc = 1
    End If
    If SliceEqualsStr(cnPos, cnLen, "Realloc", 7) Then
      cg_usesRealloc = 1
    End If
    If SliceEqualsStr(cnPos, cnLen, "Free", 4) Or SliceEqualsStr(cnPos, cnLen, "HeapFree", 8) Then
      cg_usesFree = 1
    End If
    If SliceEqualsStr(cnPos, cnLen, "Fopen", 5) Then
      cg_usesFopen = 1
    End If
    If SliceEqualsStr(cnPos, cnLen, "Getc", 4) Then
      cg_usesGetc = 1
    End If
    If SliceEqualsStr(cnPos, cnLen, "Putc", 4) Then
      cg_usesPutc = 1
    End If
  End If
  CgCollectStrings(g_nodes(id).left)
  CgCollectStrings(g_nodes(id).right)
  CgCollectStrings(g_nodes(id).third)
  For i = 0 To g_nodes(id).childCount - 1 Do
    CgCollectStrings(g_kids(g_nodes(id).firstChild + i))
  End For
  For j = 0 To g_nodes(id).extraCount - 1 Do
    CgCollectStrings(g_kids(g_nodes(id).extraStart + j))
  End For
End Sub

Private Sub CgEmitStringGlobalsRange(startIdx, endIdx As Integer)
  For i = startIdx To endIdx - 1 Do
    Dim sp As Integer = cg_strs(i).srcPos
    Dim sl As Integer = cg_strs(i).srcLen
    Dim bl As Integer = cg_strs(i).byteLen
    fprintf(cg_out, "@.str_%d = private unnamed_addr constant [%d x i8] c\"", i, bl)
    Dim j As Integer = 0
    While j < sl Do
      Dim chRaw As Integer = g_srcPool(sp + j)
      Dim ch As Integer = chRaw And 255
      If (ch = 92) And ((j + 1) < sl) Then
        Dim ncRaw As Integer = g_srcPool(sp + j + 1)
        Dim nc As Integer = ncRaw And 255
        Match nc With
          Case 110 Then fprintf(cg_out, "\\0A")
          Case 114 Then fprintf(cg_out, "\\0D")
          Case 116 Then fprintf(cg_out, "\\09")
          Case 48 Then fprintf(cg_out, "\\00")
          Case _ Then fprintf(cg_out, "\\%02X", nc)
        End Match
        j = j + 2
      Else
        If (ch >= 32) And (ch <= 126) And (ch <> 34) And (ch <> 92) Then
          fputc(ch, cg_out)
        Else
          fprintf(cg_out, "\\%02X", ch)
        End If
        j = j + 1
      End If
    End While
    fprintf(cg_out, "\\00\", align 1\n")
  End For
End Sub

Private Sub CgEmitStringGlobals()
  CgEmitStringGlobalsRange(0, cg_strCount)
End Sub

Private Sub CgAppendPoolChar(ch As Integer)
  If g_srcPoolLen < (g_srcPoolCap - 1) Then
    g_srcPool(g_srcPoolLen) = ch
    g_srcPoolLen = g_srcPoolLen + 1
  End If
End Sub

Private Sub CgAppendPoolStr(s As Pointer, slen As Integer)
  For i = 0 To slen - 1 Do
    CgAppendPoolChar(s(i))
  End For
End Sub

Private Sub CgEmitSingleHexConst(pos As Integer, slen As Integer, isNeg As Integer)
  Dim buf(64) As Byte
  Dim bi As Integer = 0
  If isNeg = 1 Then
    buf(0) = 45
    bi = 1
  End If
  Dim i As Integer = 0
  While (i < slen) And (bi < 62) Do
    buf(bi) = g_srcPool(pos + i)
    bi = bi + 1
    i = i + 1
  End While
  buf(bi) = 0
  Dim nullP As Pointer
  Dim d As Double = strtod(buf, nullP)
  Dim f As Single = d
  Dim d2 As Double = f
  Dim bits As Long = 0
  memcpy(ByRef bits, ByRef d2, 8)
  fprintf(cg_out, "0x%016llX", bits)
End Sub

Private Sub CgEmitConstInit(ty As TypeInfo, exprId As Integer)
  If exprId < 0 Then
    CgEmitZeroVal(ty)
    Return
  End If
  Dim k As Integer = g_nodes(exprId).kind
  If k = NdFloatLit Then
    If ty.kind = TySingle Then
      CgEmitSingleHexConst(g_nodes(exprId).intVal, g_nodes(exprId).lenVal, 0)
    Else
      CgEmitSlice(g_nodes(exprId).intVal, g_nodes(exprId).lenVal)
    End If
    Return
  End If
  If k = NdIntLit Then
    If (ty.kind = TyPointer) Or (ty.arrLen < 0) Then
      If g_nodes(exprId).intVal = 0 Then
        fprintf(cg_out, "null")
      Else
        If cg_ptrSize = 4 Then
          fprintf(cg_out, "inttoptr (i32 %u to ptr)", g_nodes(exprId).intVal)
        Else
          fprintf(cg_out, "inttoptr (i64 %u to ptr)", g_nodes(exprId).intVal)
        End If
      End If
    Else
      If (ty.kind = TySingle) Or (ty.kind = TyDouble) Then
        fprintf(cg_out, "%d.0", g_nodes(exprId).intVal)
      Else
        fprintf(cg_out, "%d", g_nodes(exprId).intVal)
      End If
    End If
    Return
  End If
  If (k = NdNeg) And (g_nodes(exprId).left >= 0) Then
    Dim inner As Integer = g_nodes(exprId).left
    If g_nodes(inner).kind = NdFloatLit Then
      If ty.kind = TySingle Then
        CgEmitSingleHexConst(g_nodes(inner).intVal, g_nodes(inner).lenVal, 1)
      Else
        fprintf(cg_out, "-")
        CgEmitSlice(g_nodes(inner).intVal, g_nodes(inner).lenVal)
      End If
      Return
    End If
    fprintf(cg_out, "%d", -g_nodes(inner).intVal)
    Return
  End If
  If k = NdBoolLit Then
    If g_nodes(exprId).intVal <> 0 Then
      fprintf(cg_out, "true")
    Else
      fprintf(cg_out, "false")
    End If
    Return
  End If
  If k = NdStrLit Then
    fprintf(cg_out, "@.str_%d", g_nodes(exprId).op)
    Return
  End If
  If k = NdAddressOf Then
    Dim tgt As Integer = g_nodes(exprId).left
    If (tgt >= 0) And (g_nodes(tgt).kind = NdVar) Then
      Dim fIdx As Integer = CgFindFunc(g_nodes(tgt).intVal, g_nodes(tgt).lenVal)
      If fIdx >= 0 Then
        fprintf(cg_out, "@")
        CgEmitSymName(cg_funcs(fIdx).namePos, cg_funcs(fIdx).nameLen, cg_funcs(fIdx).modId, cg_funcs(fIdx).isPrivate)
        Return
      End If
      Dim gIdx As Integer = CgFindGlobal(g_nodes(tgt).intVal, g_nodes(tgt).lenVal)
      If gIdx >= 0 Then
        If cg_globals(gIdx).hasFixedAddr = 1 Then
          If cg_ptrSize = 4 Then
            fprintf(cg_out, "inttoptr (i32 %u to ptr)", cg_globals(gIdx).fixedAddr)
          Else
            fprintf(cg_out, "inttoptr (i64 %u to ptr)", cg_globals(gIdx).fixedAddr)
          End If
        Else
          fprintf(cg_out, "@")
          CgEmitSymName(cg_globals(gIdx).namePos, cg_globals(gIdx).nameLen, cg_globals(gIdx).modId, cg_globals(gIdx).isPrivate)
        End If
        Return
      End If
    End If
  End If
  If k = NdFieldAccess Then
    Dim baseId As Integer = g_nodes(exprId).left
    If g_nodes(baseId).kind = NdVar Then
      Dim eIdx As Integer = CgFindEnum(g_nodes(baseId).intVal, g_nodes(baseId).lenVal)
      If eIdx >= 0 Then
        Dim ev As Integer = 0
        If CgFindEnumVal(eIdx, g_nodes(exprId).intVal, g_nodes(exprId).lenVal, ByRef ev) Then
          fprintf(cg_out, "%d", ev)
          Return
        End If
      End If
    End If
  End If
  If k = NdVar Then
    Dim ev As Integer = 0
    If CgFindUnqualEnumVal(g_nodes(exprId).intVal, g_nodes(exprId).lenVal, ByRef ev) Then
      fprintf(cg_out, "%d", ev)
      Return
    End If
  End If
  If (k = NdArrayLit) And (ty.arrLen > 0) Then
    Dim elemTy As TypeInfo = CgElemType(ty)
    Dim cnt As Integer = g_nodes(exprId).childCount
    Dim st As Integer = g_nodes(exprId).firstChild
    fprintf(cg_out, "[")
    Dim i As Integer = 0
    While i < ty.arrLen Do
      If i > 0 Then
        fprintf(cg_out, ", ")
      End If
      CgEmitLlvmType(elemTy)
      fprintf(cg_out, " ")
      If i < cnt Then
        CgEmitConstInit(elemTy, g_kids(st + i))
      Else
        CgEmitZeroVal(elemTy)
      End If
      i = i + 1
    End While
    fprintf(cg_out, "]")
    Return
  End If
  If (k = NdStructInit) And (ty.kind = TyStruct) And (ty.typeIdx >= 0) Then
    Dim fst As Integer = cg_structs(ty.typeIdx).fieldStart
    Dim fcnt As Integer = cg_structs(ty.typeIdx).fieldCount
    Dim ist As Integer = g_nodes(exprId).firstChild
    Dim icnt As Integer = g_nodes(exprId).childCount
    fprintf(cg_out, "{ ")
    For i = 0 To fcnt - 1 Do
      If i > 0 Then
        fprintf(cg_out, ", ")
      End If
      Dim fty As TypeInfo = cg_fields(fst + i).ty
      CgEmitLlvmType(fty)
      fprintf(cg_out, " ")
      Dim foundExpr As Integer = -1
      For j = 0 To icnt - 1 Do
        Dim fi As Integer = g_kids(ist + j)
        If SlicesEqual(g_nodes(fi).intVal, g_nodes(fi).lenVal, cg_fields(fst + i).namePos, cg_fields(fst + i).nameLen) Then
          foundExpr = g_nodes(fi).left
        End If
      End For
      CgEmitConstInit(fty, foundExpr)
    End For
    fprintf(cg_out, " }")
    Return
  End If
  CgEmitZeroVal(ty)
End Sub

Private Function CgIntBits(kind As Integer) As Integer
  Match kind With
    Case TyByte Then Return 8
    Case TyShort Then Return 16
    Case TyInt, TyEnum Then Return 32
    Case TyLong Then Return 64
    Case _ Then Return 0
  End Match
End Function

Private Function CgIsFloat(kind As Integer) As Boolean
  Return (kind = TySingle) Or (kind = TyDouble)
End Function

Private Function CgCoerceScalar(valReg As Integer, fromTy As TypeInfo, toTy As TypeInfo) As Integer
  If (toTy.kind = TyPointer) Or (toTy.kind = TyTask) Or (toTy.arrLen < 0) Then
    If (fromTy.kind = TyPointer) Or (fromTy.kind = TyTask) Or (fromTy.arrLen <> 0) Or (fromTy.kind = TyStruct) Or (fromTy.kind = TyArena) Then
      Return valReg
    End If
    Dim fb As Integer = CgIntBits(fromTy.kind)
    If fb > 0 Then
      Dim r As Integer = CgNextReg()
      fprintf(cg_out, "  %%r%d = inttoptr i%d %%r%d to ptr\n", r, fb, valReg)
      Return r
    End If
    Return valReg
  End If

  If (fromTy.kind = TyDouble) And (toTy.kind = TySingle) Then
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = fptrunc double %%r%d to float\n", r, valReg)
    Return r
  End If
  If (fromTy.kind = TySingle) And (toTy.kind = TyDouble) Then
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = fpext float %%r%d to double\n", r, valReg)
    Return r
  End If

  Dim fromBits As Integer = CgIntBits(fromTy.kind)
  Dim toBits As Integer = CgIntBits(toTy.kind)
  If (fromBits > 0) And CgIsFloat(toTy.kind) Then
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = sitofp i%d %%r%d to ", r, fromBits, valReg)
    CgEmitLlvmType(toTy)
    fprintf(cg_out, "\n")
    Return r
  End If
  If CgIsFloat(fromTy.kind) And (toBits > 0) Then
    If toBits < 32 Then
      Dim tmp As Integer = CgNextReg()
      fprintf(cg_out, "  %%r%d = fptosi ", tmp)
      CgEmitLlvmType(fromTy)
      fprintf(cg_out, " %%r%d to i32\n", valReg)
      Dim r As Integer = CgNextReg()
      fprintf(cg_out, "  %%r%d = trunc i32 %%r%d to i%d\n", r, tmp, toBits)
      Return r
    End If
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = fptosi ", r)
    CgEmitLlvmType(fromTy)
    fprintf(cg_out, " %%r%d to i%d\n", valReg, toBits)
    Return r
  End If

  If (fromBits > 0) And (toBits > 0) And (fromBits <> toBits) Then
    Dim r As Integer = CgNextReg()
    If toBits > fromBits Then
      fprintf(cg_out, "  %%r%d = sext i%d %%r%d to i%d\n", r, fromBits, valReg, toBits)
    Else
      fprintf(cg_out, "  %%r%d = trunc i%d %%r%d to i%d\n", r, fromBits, valReg, toBits)
    End If
    Return r
  End If
  Return valReg
End Function

Private Function CgWidenToI64(valReg As Integer, fromTy As TypeInfo) As Integer
  Dim longTy As TypeInfo With ( kind = TyLong, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
  Return CgCoerceScalar(valReg, fromTy, longTy)
End Function

' C default argument promotions for variadic (...) calls (float->double, i1/i8/i16->i32)
Private Function CgPromoteVariadic(aReg As Integer, ByRef aTy As TypeInfo) As Integer
  If aTy.arrLen <> 0 Then
    Return aReg
  End If
  If aTy.kind = TySingle Then
    Dim dblTy As TypeInfo With ( kind = TyDouble, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    Dim r As Integer = CgCoerceScalar(aReg, aTy, dblTy)
    aTy = dblTy
    Return r
  End If
  If aTy.kind = TyBoolean Then
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = zext i1 %%r%d to i32\n", r, aReg)
    aTy = TypeInfo With ( kind = TyInt, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    Return r
  End If
  If (aTy.kind = TyByte) Or (aTy.kind = TyShort) Then
    Dim intTy As TypeInfo With ( kind = TyInt, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    Dim r As Integer = CgCoerceScalar(aReg, aTy, intTy)
    aTy = intTy
    Return r
  End If
  Return aReg
End Function

Private Sub CgEmitStoreTypedEx(dstPtrReg As Integer, dstTy As TypeInfo, valReg As Integer, valTy As TypeInfo, isVol As Integer)
  If CgIsAggregate(dstTy) Then
    Dim sz As Integer = CgTypeSize(dstTy)
    Dim al As Integer = CgTypeAlign(dstTy)
    If isVol = 1 Then
      fprintf(cg_out, "  call void @llvm.memcpy.p0.p0.i64(ptr align %d %%r%d, ptr align %d %%r%d, i64 %d, i1 true)\n", al, dstPtrReg, al, valReg, sz)
    Else
      fprintf(cg_out, "  call void @llvm.memcpy.p0.p0.i64(ptr align %d %%r%d, ptr align %d %%r%d, i64 %d, i1 false)\n", al, dstPtrReg, al, valReg, sz)
    End If
    Return
  End If
  Dim coerced As Integer = CgCoerceScalar(valReg, valTy, dstTy)
  Dim align As Integer = CgTypeAlign(dstTy)
  If isVol = 1 Then
    fprintf(cg_out, "  store volatile ")
  Else
    fprintf(cg_out, "  store ")
  End If
  CgEmitLlvmType(dstTy)
  fprintf(cg_out, " %%r%d, ptr %%r%d, align %d\n", coerced, dstPtrReg, align)
End Sub

Private Sub CgEmitStoreTyped(dstPtrReg As Integer, dstTy As TypeInfo, valReg As Integer, valTy As TypeInfo)
  CgEmitStoreTypedEx(dstPtrReg, dstTy, valReg, valTy, 0)
End Sub

Private Function CgIsVarIndexCall(id As Integer) As Boolean
  If (g_nodes(id).kind <> NdCall) Or (g_nodes(id).childCount <> 1) Then
    Return False
  End If
  Dim argNode As Integer = g_kids(g_nodes(id).firstChild)
  If g_nodes(argNode).op <> 0 Then
    Return False
  End If
  Dim pos As Integer = g_nodes(id).intVal
  Dim slen As Integer = g_nodes(id).lenVal
  Dim lIdx As Integer = CgFindLocal(pos, slen)
  If lIdx >= 0 Then
    Return True
  End If
  Dim gIdx As Integer = CgFindGlobal(pos, slen)
  If gIdx >= 0 Then
    Return True
  End If
  Return False
End Function

' Returns %rN pointer register to the lvalue storage + fills outTy, outIsIndirect & outIsVolatile
Private Function CgEmitLValuePtr(id As Integer, ByRef outTy As TypeInfo, ByRef outIsIndirect, outIsVolatile As Integer) As Integer
  outIsIndirect = 0
  outIsVolatile = 0
  Dim k As Integer = g_nodes(id).kind

  If k = NdWithTarget Then
    outTy = cg_withTys(cg_withDepth - 1)
    outIsIndirect = 0
    outIsVolatile = cg_withVols(cg_withDepth - 1)
    Return cg_withPtrs(cg_withDepth - 1)
  End If

  If k = NdVar Then
    Dim pos As Integer = g_nodes(id).intVal
    Dim slen As Integer = g_nodes(id).lenVal
    Dim lIdx As Integer = CgFindLocal(pos, slen)
    If lIdx >= 0 Then
      outTy = cg_locals(lIdx).ty
      outIsIndirect = cg_locals(lIdx).isIndirect
      outIsVolatile = cg_locals(lIdx).isVolatile
      If cg_locals(lIdx).staticPlusOne > 0 Then
        Dim r As Integer = CgNextReg()
        fprintf(cg_out, "  %%r%d = getelementptr i8, ptr @__vb_static_%d, i64 0\n", r, cg_locals(lIdx).staticPlusOne - 1)
        Return r
      End If
      Dim sr As Integer = cg_locals(lIdx).slotReg
      If sr < 0 Then
        Dim pIdx As Integer = -(sr + 1)
        Dim r As Integer = CgNextReg()
        fprintf(cg_out, "  %%r%d = getelementptr i8, ptr %%p%d, i64 0\n", r, pIdx)
        Return r
      End If
      Return sr
    End If
    Dim gIdx As Integer = CgFindGlobal(pos, slen)
    If gIdx >= 0 Then
      outTy = cg_globals(gIdx).ty
      outIsVolatile = cg_globals(gIdx).isVolatile
      If outTy.arrLen < 0 Then
        outIsIndirect = 1
      End If
      If cg_globals(gIdx).hasFixedAddr = 1 Then
        Dim r As Integer = CgNextReg()
        If cg_ptrSize = 4 Then
          fprintf(cg_out, "  %%r%d = inttoptr i32 %u to ptr\n", r, cg_globals(gIdx).fixedAddr)
        Else
          fprintf(cg_out, "  %%r%d = inttoptr i64 %u to ptr\n", r, cg_globals(gIdx).fixedAddr)
        End If
        Return r
      End If
      Dim r As Integer = CgNextReg()
      fprintf(cg_out, "  %%r%d = getelementptr i8, ptr @", r)
      CgEmitSymName(pos, slen, cg_globals(gIdx).modId, cg_globals(gIdx).isPrivate)
      fprintf(cg_out, ", i64 0\n")
      Return r
    End If
    Printf(MSG_CG_UNDEF_VAR)
    Return 0
  End If

  If k = NdFieldAccess Then
    Dim baseTy As TypeInfo
    Dim baseInd = 0, baseVol = 0 As Integer
    Dim basePtr As Integer = CgEmitLValuePtr(g_nodes(id).left, ByRef baseTy, ByRef baseInd, ByRef baseVol)
    Dim fTy As TypeInfo
    Dim fIdx As Integer = CgFindStructField(baseTy.typeIdx, g_nodes(id).intVal, g_nodes(id).lenVal, ByRef fTy)
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = getelementptr inbounds ", r)
    CgEmitBaseLlvmType(baseTy.kind, baseTy.typeIdx, baseTy.namePos, baseTy.nameLen)
    fprintf(cg_out, ", ptr %%r%d, i32 0, i32 %d\n", basePtr, fIdx)
    outTy = fTy
    outIsVolatile = baseVol
    Return r
  End If

  If (k = NdIndexAccess) Or CgIsVarIndexCall(id) Then
    Dim baseNode As Integer = -1
    Dim idxNode As Integer = -1
    If k = NdIndexAccess Then
      baseNode = g_nodes(id).left
      idxNode = g_nodes(id).right
    Else
      Dim argNode As Integer = g_kids(g_nodes(id).firstChild)
      idxNode = g_nodes(argNode).left
      baseNode = NewNode(NdVar)
      g_nodes(baseNode).intVal = g_nodes(id).intVal
      g_nodes(baseNode).lenVal = g_nodes(id).lenVal
    End If

    Dim idxTy As TypeInfo
    Dim idxReg As Integer = CgEmitExpr(idxNode, ByRef idxTy)
    Dim idx64 As Integer = CgWidenToI64(idxReg, idxTy)

    Dim baseTy As TypeInfo
    Dim baseInd = 0, baseVol = 0 As Integer
    Dim basePtr As Integer = CgEmitLValuePtr(baseNode, ByRef baseTy, ByRef baseInd, ByRef baseVol)
    outIsVolatile = baseVol

    If baseTy.arrLen > 0 Then
      Dim elemTy As TypeInfo = CgElemType(baseTy)
      Dim r As Integer = CgNextReg()
      fprintf(cg_out, "  %%r%d = getelementptr inbounds ", r)
      CgEmitLlvmType(baseTy)
      fprintf(cg_out, ", ptr %%r%d, i64 0, i64 %%r%d\n", basePtr, idx64)
      outTy = elemTy
      Return r
    End If

    If baseTy.arrLen < 0 Then
      Dim rawPtr As Integer = basePtr
      If baseInd = 1 Then
        rawPtr = CgNextReg()
        fprintf(cg_out, "  %%r%d = load ptr, ptr %%r%d, align %d\n", rawPtr, basePtr, cg_ptrAlign)
      End If
      Dim elemTy As TypeInfo = CgElemType(baseTy)
      Dim r As Integer = CgNextReg()
      fprintf(cg_out, "  %%r%d = getelementptr inbounds ", r)
      CgEmitLlvmType(elemTy)
      fprintf(cg_out, ", ptr %%r%d, i64 %%r%d\n", rawPtr, idx64)
      outTy = elemTy
      Return r
    End If

    If baseTy.kind = TyPointer Then
      Dim rawPtr As Integer = CgNextReg()
      fprintf(cg_out, "  %%r%d = load ptr, ptr %%r%d, align %d\n", rawPtr, basePtr, cg_ptrAlign)
      Dim r As Integer = CgNextReg()
      fprintf(cg_out, "  %%r%d = getelementptr inbounds i8, ptr %%r%d, i64 %%r%d\n", r, rawPtr, idx64)
      outTy = TypeInfo With ( kind = TyByte, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
      Return r
    End If
  End If

  Printf(MSG_CG_INVALID_LVAL)
  Return 0
End Function

Private Function CgIsLValueNode(id As Integer) As Boolean
  If id < 0 Then
    Return False
  End If
  Dim k As Integer = g_nodes(id).kind
  If (k = NdVar) Or (k = NdWithTarget) Then
    Return True
  End If
  If k = NdCall Then
    Return CgIsVarIndexCall(id)
  End If
  If (k = NdFieldAccess) Or (k = NdIndexAccess) Then
    Return CgIsLValueNode(g_nodes(id).left)
  End If
  Return False
End Function

Private Function CgEmitByRefArg(exprId As Integer) As Integer
  Dim ty As TypeInfo
  Dim isInd = 0, isVol = 0 As Integer
  Dim ptrReg As Integer = CgEmitLValuePtr(exprId, ByRef ty, ByRef isInd, ByRef isVol)
  If (g_nodes(exprId).kind = NdVar) And (isInd = 1) Then
    Dim loaded As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = load ptr, ptr %%r%d, align %d\n", loaded, ptrReg, cg_ptrAlign)
    Return loaded
  End If
  Return ptrReg
End Function

Private Function CgEmitExpr(id As Integer, ByRef outTy As TypeInfo) As Integer
  Dim k As Integer = g_nodes(id).kind

  If k = NdIntLit Then
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = add i32 0, %d\n", r, g_nodes(id).intVal)
    outTy = TypeInfo With ( kind = TyInt, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    Return r
  End If

  If k = NdFloatLit Then
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = fadd double 0.0, ", r)
    CgEmitSlice(g_nodes(id).intVal, g_nodes(id).lenVal)
    fprintf(cg_out, "\n")
    outTy = TypeInfo With ( kind = TyDouble, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    Return r
  End If

  If k = NdBoolLit Then
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = add i1 0, %d\n", r, g_nodes(id).intVal)
    outTy = TypeInfo With ( kind = TyBoolean, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    Return r
  End If

  If k = NdStrLit Then
    Dim sid As Integer = g_nodes(id).op
    Dim bl As Integer = cg_strs(sid).byteLen
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = getelementptr inbounds [%d x i8], ptr @.str_%d, i64 0, i64 0\n", r, bl, sid)
    outTy = TypeInfo With ( kind = TyPointer, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    Return r
  End If

  If k = NdStrInterp Then
    cg_usesStrInterp = 1
    cg_usesCalloc = 1
    Dim pStart As Integer = g_nodes(id).firstChild
    Dim pCount As Integer = g_nodes(id).childCount
    Dim argRegs(64), argKinds(64) As Integer
    Dim argTys(64) As TypeInfo
    Dim argCount As Integer = 0
    Dim ptrTy As TypeInfo With ( kind = TyPointer, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )

    ' Pass 1: Evaluate all interpolated expressions before writing format string to g_srcPool
    For pi = 0 To pCount - 1 Do
      Dim ch As Integer = g_kids(pStart + pi)
      If g_nodes(ch).kind <> NdStrLit Then
        Dim aTy As TypeInfo
        Dim aReg As Integer = CgEmitExpr(ch, ByRef aTy)
        If (aTy.arrLen = 0) And (aTy.kind = TyBoolean) Then
          Dim bSel As Integer = CgNextReg()
          fprintf(cg_out, "  %%r%d = select i1 %%r%d, ptr @__vb_true_str, ptr @__vb_false_str\n", bSel, aReg)
          argRegs(argCount) = bSel
          argTys(argCount) = ptrTy
          argKinds(argCount) = 4
        Else
          If ((aTy.arrLen = 0) And (aTy.kind = TyPointer)) Or ((aTy.arrLen <> 0) And (aTy.kind = TyByte)) Then
            Dim nCmp As Integer = CgNextReg()
            Dim nSel As Integer = CgNextReg()
            fprintf(cg_out, "  %%r%d = icmp eq ptr %%r%d, null\n", nCmp, aReg)
            fprintf(cg_out, "  %%r%d = select i1 %%r%d, ptr @__vb_empty_str, ptr %%r%d\n", nSel, nCmp, aReg)
            argRegs(argCount) = nSel
            argTys(argCount) = ptrTy
            argKinds(argCount) = 4
          Else
            If aTy.kind = TyLong Then
              argRegs(argCount) = aReg
              argTys(argCount) = aTy
              argKinds(argCount) = 2
            Else
              If (aTy.kind = TySingle) Or (aTy.kind = TyDouble) Then
                aReg = CgPromoteVariadic(aReg, ByRef aTy)
                argRegs(argCount) = aReg
                argTys(argCount) = aTy
                argKinds(argCount) = 3
              Else
                aReg = CgPromoteVariadic(aReg, ByRef aTy)
                argRegs(argCount) = aReg
                argTys(argCount) = aTy
                argKinds(argCount) = 1
              End If
            End If
          End If
        End If
        argCount = argCount + 1
      End If
    End For

    ' Pass 2: Build synthesized printf format string contiguously in g_srcPool
    Dim fmtStart As Integer = g_srcPoolLen
    Dim ai As Integer = 0
    For pi = 0 To pCount - 1 Do
      Dim ch As Integer = g_kids(pStart + pi)
      If g_nodes(ch).kind = NdStrLit Then
        Dim sp = g_nodes(ch).intVal, sl = g_nodes(ch).lenVal, j = 0 As Integer
        While j < sl Do
          Dim b As Integer = g_srcPool(sp + j)
          If (b = 92) And ((j + 1) < sl) Then
            CgAppendPoolChar(b)
            CgAppendPoolChar(g_srcPool(sp + j + 1))
            j = j + 2
          Else
            If b = 37 Then
              CgAppendPoolChar(37)
              CgAppendPoolChar(37)
            Else
              CgAppendPoolChar(b)
            End If
            j = j + 1
          End If
        End While
      Else
        Match argKinds(ai) With
          Case 2 Then CgAppendPoolStr("%lld", 4)
          Case 3 Then CgAppendPoolStr("%g", 2)
          Case 4 Then CgAppendPoolStr("%s", 2)
          Case _ Then CgAppendPoolStr("%d", 2)
        End Match
        ai = ai + 1
      End If
    End For
    Dim fmtLen As Integer = g_srcPoolLen - fmtStart
    Dim fmtSid As Integer = CgRegisterString(fmtStart, fmtLen)
    Dim fmtBl As Integer = cg_strs(fmtSid).byteLen

    ' Pass 3: Exact-sized two-pass snprintf + calloc
    Dim fmtPtr As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = getelementptr inbounds [%d x i8], ptr @.str_%d, i64 0, i64 0\n", fmtPtr, fmtBl, fmtSid)
    Dim szTyStr As Pointer = "i64"
    If cg_ptrSize = 4 Then
      szTyStr = "i32"
    End If
    Dim lenReg As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = call i32 (ptr, %s, ptr, ...) @snprintf(ptr null, %s 0, ptr %%r%d", lenReg, szTyStr, szTyStr, fmtPtr)
    ai = 0
    While ai < argCount Do
      fprintf(cg_out, ", ")
      CgEmitLlvmType(argTys(ai))
      fprintf(cg_out, " %%r%d", argRegs(ai))
      ai = ai + 1
    End While
    fprintf(cg_out, ")\n")

    Dim posCmp = CgNextReg(), safeLen = CgNextReg(), len64 = CgNextReg(), tot64 = CgNextReg(), bufReg = CgNextReg() As Integer
    fprintf(cg_out, "  %%r%d = icmp sgt i32 %%r%d, 0\n", posCmp, lenReg)
    fprintf(cg_out, "  %%r%d = select i1 %%r%d, i32 %%r%d, i32 0\n", safeLen, posCmp, lenReg)
    fprintf(cg_out, "  %%r%d = sext i32 %%r%d to i64\n", len64, safeLen)
    fprintf(cg_out, "  %%r%d = add i64 %%r%d, 1\n", tot64, len64)
    fprintf(cg_out, "  %%r%d = call ptr @calloc(i64 %%r%d, i64 1)\n", bufReg, tot64)

    Dim nullCmp = CgNextReg(), lFill = CgNextLbl(), lDone = CgNextLbl() As Integer
    fprintf(cg_out, "  %%r%d = icmp eq ptr %%r%d, null\n", nullCmp, bufReg)
    fprintf(cg_out, "  br i1 %%r%d, label %%L%d, label %%L%d\n", nullCmp, lDone, lFill)
    fprintf(cg_out, "L%d:\n", lFill)
    Dim totSzReg As Integer = tot64
    If cg_ptrSize = 4 Then
      totSzReg = CgNextReg()
      fprintf(cg_out, "  %%r%d = trunc i64 %%r%d to i32\n", totSzReg, tot64)
    End If
    fprintf(cg_out, "  call i32 (ptr, %s, ptr, ...) @snprintf(ptr %%r%d, %s %%r%d, ptr %%r%d", szTyStr, bufReg, szTyStr, totSzReg, fmtPtr)
    ai = 0
    While ai < argCount Do
      fprintf(cg_out, ", ")
      CgEmitLlvmType(argTys(ai))
      fprintf(cg_out, " %%r%d", argRegs(ai))
      ai = ai + 1
    End While
    fprintf(cg_out, ")\n")
    fprintf(cg_out, "  br label %%L%d\n", lDone)
    fprintf(cg_out, "L%d:\n", lDone)
    outTy = ptrTy
    Return bufReg
  End If

  If k = NdWithTarget Then
    outTy = cg_withTys(cg_withDepth - 1)
    Return cg_withPtrs(cg_withDepth - 1)
  End If

  If k = NdVar Then
    Dim vPos As Integer = g_nodes(id).intVal
    Dim vLen As Integer = g_nodes(id).lenVal
    If (CgFindLocal(vPos, vLen) < 0) And (CgFindGlobal(vPos, vLen) < 0) Then
      Dim ev As Integer = 0
      If CgFindUnqualEnumVal(vPos, vLen, ByRef ev) Then
        Dim r As Integer = CgNextReg()
        fprintf(cg_out, "  %%r%d = add i32 0, %d\n", r, ev)
        outTy = TypeInfo With ( kind = TyInt, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
        Return r
      End If
    End If
    Dim vTy As TypeInfo
    Dim isInd = 0, isVol = 0 As Integer
    Dim ptrReg As Integer = CgEmitLValuePtr(id, ByRef vTy, ByRef isInd, ByRef isVol)
    outTy = vTy
    If CgIsAggregate(vTy) Then
      Return ptrReg
    End If
    If vTy.arrLen < 0 Then
      If isInd = 1 Then
        Dim r As Integer = CgNextReg()
        If isVol = 1 Then
          fprintf(cg_out, "  %%r%d = load volatile ptr, ptr %%r%d, align %d\n", r, ptrReg, cg_ptrAlign)
        Else
          fprintf(cg_out, "  %%r%d = load ptr, ptr %%r%d, align %d\n", r, ptrReg, cg_ptrAlign)
        End If
        Return r
      End If
      Return ptrReg
    End If
    Dim r As Integer = CgNextReg()
    Dim align As Integer = CgTypeAlign(vTy)
    If isVol = 1 Then
      fprintf(cg_out, "  %%r%d = load volatile ", r)
    Else
      fprintf(cg_out, "  %%r%d = load ", r)
    End If
    CgEmitLlvmType(vTy)
    fprintf(cg_out, ", ptr %%r%d, align %d\n", ptrReg, align)
    Return r
  End If

  If k = NdFieldAccess Then
    ' Check EnumName.Variant first
    Dim baseId As Integer = g_nodes(id).left
    If g_nodes(baseId).kind = NdVar Then
      Dim eIdx As Integer = CgFindEnum(g_nodes(baseId).intVal, g_nodes(baseId).lenVal)
      If eIdx >= 0 Then
        Dim ev As Integer = 0
        If CgFindEnumVal(eIdx, g_nodes(id).intVal, g_nodes(id).lenVal, ByRef ev) Then
          Dim r As Integer = CgNextReg()
          fprintf(cg_out, "  %%r%d = add i32 0, %d\n", r, ev)
          outTy = TypeInfo With ( kind = TyInt, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
          Return r
        End If
      End If
    End If
    Dim fTy As TypeInfo
    Dim isInd = 0, isVol = 0 As Integer
    Dim fPtr As Integer = 0
    If CgIsLValueNode(id) Then
      fPtr = CgEmitLValuePtr(id, ByRef fTy, ByRef isInd, ByRef isVol)
    Else
      ' Rvalue member access (e.g. GetMousePosition().x, GetShip().tf.pos.z, (Await t).field)
      Dim baseTy As TypeInfo
      Dim basePtr As Integer = CgEmitExpr(baseId, ByRef baseTy)
      Dim fIdx As Integer = CgFindStructField(baseTy.typeIdx, g_nodes(id).intVal, g_nodes(id).lenVal, ByRef fTy)
      fPtr = CgNextReg()
      fprintf(cg_out, "  %%r%d = getelementptr inbounds ", fPtr)
      CgEmitBaseLlvmType(baseTy.kind, baseTy.typeIdx, baseTy.namePos, baseTy.nameLen)
      fprintf(cg_out, ", ptr %%r%d, i32 0, i32 %d\n", basePtr, fIdx)
    End If
    outTy = fTy
    If CgIsAggregate(fTy) Then
      Return fPtr
    End If
    Dim r As Integer = CgNextReg()
    Dim align As Integer = CgTypeAlign(fTy)
    If isVol = 1 Then
      fprintf(cg_out, "  %%r%d = load volatile ", r)
    Else
      fprintf(cg_out, "  %%r%d = load ", r)
    End If
    CgEmitLlvmType(fTy)
    fprintf(cg_out, ", ptr %%r%d, align %d\n", fPtr, align)
    Return r
  End If

  If (k = NdIndexAccess) Or CgIsVarIndexCall(id) Then
    Dim eTy As TypeInfo
    Dim isInd = 0, isVol = 0 As Integer
    Dim ePtr As Integer = 0
    If CgIsLValueNode(id) Then
      ePtr = CgEmitLValuePtr(id, ByRef eTy, ByRef isInd, ByRef isVol)
    Else
      ' Rvalue indexing (e.g. GetShip().tags(0), GetSlice()(i), GetName()(0))
      Dim idxTy As TypeInfo
      Dim idxReg As Integer = CgEmitExpr(g_nodes(id).right, ByRef idxTy)
      Dim idx64 As Integer = CgWidenToI64(idxReg, idxTy)
      Dim baseTy As TypeInfo
      Dim baseVal As Integer = CgEmitExpr(g_nodes(id).left, ByRef baseTy)
      If baseTy.arrLen > 0 Then
        eTy = CgElemType(baseTy)
        ePtr = CgNextReg()
        fprintf(cg_out, "  %%r%d = getelementptr inbounds ", ePtr)
        CgEmitLlvmType(baseTy)
        fprintf(cg_out, ", ptr %%r%d, i64 0, i64 %%r%d\n", baseVal, idx64)
      Else
        If baseTy.arrLen < 0 Then
          eTy = CgElemType(baseTy)
          ePtr = CgNextReg()
          fprintf(cg_out, "  %%r%d = getelementptr inbounds ", ePtr)
          CgEmitLlvmType(eTy)
          fprintf(cg_out, ", ptr %%r%d, i64 %%r%d\n", baseVal, idx64)
        Else
          eTy = TypeInfo With ( kind = TyByte, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
          ePtr = CgNextReg()
          fprintf(cg_out, "  %%r%d = getelementptr inbounds i8, ptr %%r%d, i64 %%r%d\n", ePtr, baseVal, idx64)
        End If
      End If
    End If
    outTy = eTy
    If CgIsAggregate(eTy) Then
      Return ePtr
    End If
    Dim r As Integer = CgNextReg()
    Dim align As Integer = CgTypeAlign(eTy)
    If isVol = 1 Then
      fprintf(cg_out, "  %%r%d = load volatile ", r)
    Else
      fprintf(cg_out, "  %%r%d = load ", r)
    End If
    CgEmitLlvmType(eTy)
    fprintf(cg_out, ", ptr %%r%d, align %d\n", ePtr, align)
    Return r
  End If

  If k = NdNeg Then
    Dim t As TypeInfo
    Dim v As Integer = CgEmitExpr(g_nodes(id).left, ByRef t)
    If (t.arrLen = 0) And (t.kind = TyStruct) Then
      Dim uoi As Integer = CgFindUnaryOp(TkMinus, t)
      If uoi >= 0 Then
        Return CgEmitOpCall1(uoi, v, t, ByRef outTy)
      End If
    End If
    Dim r As Integer = CgNextReg()
    If CgIsFloat(t.kind) Then
      fprintf(cg_out, "  %%r%d = fneg ", r)
      CgEmitLlvmType(t)
      fprintf(cg_out, " %%r%d\n", v)
    Else
      fprintf(cg_out, "  %%r%d = sub nsw ", r)
      CgEmitLlvmType(t)
      fprintf(cg_out, " 0, %%r%d\n", v)
    End If
    outTy = t
    Return r
  End If

  If k = NdNot Then
    Dim t As TypeInfo
    Dim v As Integer = CgEmitExpr(g_nodes(id).left, ByRef t)
    If (t.arrLen = 0) And (t.kind = TyStruct) Then
      Dim uoi As Integer = CgFindUnaryOp(TkNot, t)
      If uoi >= 0 Then
        Return CgEmitOpCall1(uoi, v, t, ByRef outTy)
      End If
    End If
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = xor ", r)
    CgEmitLlvmType(t)
    fprintf(cg_out, " %%r%d, -1\n", v)
    outTy = t
    Return r
  End If

  If k = NdBinOp Then
    Dim op As Integer = g_nodes(id).op
    Dim t1 As TypeInfo
    Dim t2 As TypeInfo
    Dim v1 As Integer = CgEmitExpr(g_nodes(id).left, ByRef t1)
    ' Short-circuit boolean And / Or using stack-free trampoline phi blocks.
    ' Integer operands keep bitwise `and` / `or` semantics below.
    If ((op = TkAnd) Or (op = TkOr)) And (t1.kind = TyBoolean) And (t1.arrLen = 0) And (Not CgHasBoolStructOp(op)) Then
      Dim scLbl As Integer = CgNextLbl()
      If op = TkAnd Then
        fprintf(cg_out, "  br i1 %%r%d, label %%sc_rhs_%d, label %%sc_short_%d\n", v1, scLbl, scLbl)
      Else
        fprintf(cg_out, "  br i1 %%r%d, label %%sc_short_%d, label %%sc_rhs_%d\n", v1, scLbl, scLbl)
      End If
      fprintf(cg_out, "sc_short_%d:\n", scLbl)
      fprintf(cg_out, "  br label %%sc_end_%d\n", scLbl)
      fprintf(cg_out, "sc_rhs_%d:\n", scLbl)
      Dim v2Sc As Integer = CgEmitExpr(g_nodes(id).right, ByRef t2)
      If t2.kind <> TyBoolean Then
        Dim b2Bits As Integer = CgIntBits(t2.kind)
        If b2Bits > 0 Then
          Dim cv As Integer = CgNextReg()
          fprintf(cg_out, "  %%r%d = icmp ne i%d %%r%d, 0\n", cv, b2Bits, v2Sc)
          v2Sc = cv
        End If
      End If
      fprintf(cg_out, "  br label %%sc_rhs_end_%d\n", scLbl)
      fprintf(cg_out, "sc_rhs_end_%d:\n", scLbl)
      fprintf(cg_out, "  br label %%sc_end_%d\n", scLbl)
      fprintf(cg_out, "sc_end_%d:\n", scLbl)
      Dim scRes As Integer = CgNextReg()
      If op = TkAnd Then
        fprintf(cg_out, "  %%r%d = phi i1 [ false, %%sc_short_%d ], [ %%r%d, %%sc_rhs_end_%d ]\n", scRes, scLbl, v2Sc, scLbl)
      Else
        fprintf(cg_out, "  %%r%d = phi i1 [ true, %%sc_short_%d ], [ %%r%d, %%sc_rhs_end_%d ]\n", scRes, scLbl, v2Sc, scLbl)
      End If
      outTy = TypeInfo With ( kind = TyBoolean, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
      Return scRes
    End If
    Dim v2 As Integer = CgEmitExpr(g_nodes(id).right, ByRef t2)
    If ((t1.arrLen = 0) And (t1.kind = TyStruct)) Or ((t2.arrLen = 0) And (t2.kind = TyStruct)) Then
      Dim boi As Integer = CgFindBinOp(op, t1, g_nodes(id).left, t2, g_nodes(id).right)
      If boi >= 0 Then
        Return CgEmitOpCall2(boi, v1, t1, v2, t2, ByRef outTy)
      End If
    End If
    Dim s1 As Boolean = ((t1.arrLen = 0) And (t1.kind = TyPointer)) Or ((t1.arrLen <> 0) And (t1.kind = TyByte))
    Dim s2 As Boolean = ((t2.arrLen = 0) And (t2.kind = TyPointer)) Or ((t2.arrLen <> 0) And (t2.kind = TyByte))
    If (op = TkAmp) Or ((op = TkPlus) And s1 And s2) Then
      cg_usesStrConcat = 1
      Dim sr As Integer = CgNextReg()
      fprintf(cg_out, "  %%r%d = call ptr @__vb_str_concat(ptr %%r%d, ptr %%r%d)\n", sr, v1, v2)
      outTy = TypeInfo With ( kind = TyPointer, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
      Return sr
    End If
    Dim unified As TypeInfo = t1
    If CgIsFloat(t1.kind) Or CgIsFloat(t2.kind) Then
      If (t1.kind = TySingle) And (t2.kind = TyDouble) And (g_nodes(g_nodes(id).right).kind = NdFloatLit) Then
        v2 = CgCoerceScalar(v2, t2, t1)
        unified = t1
      Else
        If (t2.kind = TySingle) And (t1.kind = TyDouble) And (g_nodes(g_nodes(id).left).kind = NdFloatLit) Then
          v1 = CgCoerceScalar(v1, t1, t2)
          unified = t2
        Else
          If t1.kind <> t2.kind Then
            If (t1.kind = TyDouble) Or (t2.kind = TyDouble) Then
              unified = TypeInfo With ( kind = TyDouble, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
            Else
              unified = TypeInfo With ( kind = TySingle, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
            End If
            v1 = CgCoerceScalar(v1, t1, unified)
            v2 = CgCoerceScalar(v2, t2, unified)
          End If
        End If
      End If
    Else
      ' Contextual literal adaptation (e.g. Byte/Long op IntLit)
      Dim b1 As Integer = CgIntBits(t1.kind)
      Dim b2 As Integer = CgIntBits(t2.kind)
      If (b1 > 0) And (b2 > 0) And (b1 <> b2) Then
        If g_nodes(g_nodes(id).right).kind = NdIntLit Then
          v2 = CgCoerceScalar(v2, t2, t1)
          unified = t1
        Else
          If g_nodes(g_nodes(id).left).kind = NdIntLit Then
            v1 = CgCoerceScalar(v1, t1, t2)
            unified = t2
          Else
            If b2 > b1 Then
              v1 = CgCoerceScalar(v1, t1, t2)
              unified = t2
            Else
              v2 = CgCoerceScalar(v2, t2, t1)
              unified = t1
            End If
          End If
        End If
      End If
    End If

    Dim r As Integer = CgNextReg()
    outTy = unified
    If CgIsFloat(unified.kind) Then
      Match op With
        Case TkPlus Then
          fprintf(cg_out, "  %%r%d = fadd ", r)
          CgEmitLlvmType(unified)
          fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
        Case TkMinus Then
          fprintf(cg_out, "  %%r%d = fsub ", r)
          CgEmitLlvmType(unified)
          fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
        Case TkStar Then
          fprintf(cg_out, "  %%r%d = fmul ", r)
          CgEmitLlvmType(unified)
          fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
        Case TkSlash Then
          fprintf(cg_out, "  %%r%d = fdiv ", r)
          CgEmitLlvmType(unified)
          fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
        Case TkMod Then
          fprintf(cg_out, "  %%r%d = frem ", r)
          CgEmitLlvmType(unified)
          fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
        Case TkEq Then
          fprintf(cg_out, "  %%r%d = fcmp oeq ", r)
          CgEmitLlvmType(unified)
          fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
          outTy = TypeInfo With ( kind = TyBoolean, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
        Case TkNeq Then
          fprintf(cg_out, "  %%r%d = fcmp one ", r)
          CgEmitLlvmType(unified)
          fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
          outTy = TypeInfo With ( kind = TyBoolean, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
        Case TkLt Then
          fprintf(cg_out, "  %%r%d = fcmp olt ", r)
          CgEmitLlvmType(unified)
          fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
          outTy = TypeInfo With ( kind = TyBoolean, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
        Case TkGt Then
          fprintf(cg_out, "  %%r%d = fcmp ogt ", r)
          CgEmitLlvmType(unified)
          fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
          outTy = TypeInfo With ( kind = TyBoolean, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
        Case TkLe Then
          fprintf(cg_out, "  %%r%d = fcmp ole ", r)
          CgEmitLlvmType(unified)
          fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
          outTy = TypeInfo With ( kind = TyBoolean, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
        Case TkGe Then
          fprintf(cg_out, "  %%r%d = fcmp oge ", r)
          CgEmitLlvmType(unified)
          fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
          outTy = TypeInfo With ( kind = TyBoolean, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
      End Match
      Return r
    End If
    Match op With
      Case TkPlus Then
        fprintf(cg_out, "  %%r%d = add nsw ", r)
        CgEmitLlvmType(unified)
        fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
      Case TkMinus Then
        fprintf(cg_out, "  %%r%d = sub nsw ", r)
        CgEmitLlvmType(unified)
        fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
      Case TkStar Then
        fprintf(cg_out, "  %%r%d = mul nsw ", r)
        CgEmitLlvmType(unified)
        fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
      Case TkSlash Then
        fprintf(cg_out, "  %%r%d = sdiv ", r)
        CgEmitLlvmType(unified)
        fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
      Case TkMod Then
        fprintf(cg_out, "  %%r%d = srem ", r)
        CgEmitLlvmType(unified)
        fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
      Case TkAnd Then
        fprintf(cg_out, "  %%r%d = and ", r)
        CgEmitLlvmType(unified)
        fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
      Case TkOr Then
        fprintf(cg_out, "  %%r%d = or ", r)
        CgEmitLlvmType(unified)
        fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
      Case TkXor Then
        fprintf(cg_out, "  %%r%d = xor ", r)
        CgEmitLlvmType(unified)
        fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
      Case TkShl Then
        fprintf(cg_out, "  %%r%d = shl ", r)
        CgEmitLlvmType(unified)
        fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
      Case TkShr Then
        fprintf(cg_out, "  %%r%d = lshr ", r)
        CgEmitLlvmType(unified)
        fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
      Case TkEq Then
        fprintf(cg_out, "  %%r%d = icmp eq ", r)
        CgEmitLlvmType(unified)
        fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
        outTy = TypeInfo With ( kind = TyBoolean, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
      Case TkNeq Then
        fprintf(cg_out, "  %%r%d = icmp ne ", r)
        CgEmitLlvmType(unified)
        fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
        outTy = TypeInfo With ( kind = TyBoolean, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
      Case TkLt Then
        fprintf(cg_out, "  %%r%d = icmp slt ", r)
        CgEmitLlvmType(unified)
        fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
        outTy = TypeInfo With ( kind = TyBoolean, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
      Case TkGt Then
        fprintf(cg_out, "  %%r%d = icmp sgt ", r)
        CgEmitLlvmType(unified)
        fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
        outTy = TypeInfo With ( kind = TyBoolean, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
      Case TkLe Then
        fprintf(cg_out, "  %%r%d = icmp sle ", r)
        CgEmitLlvmType(unified)
        fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
        outTy = TypeInfo With ( kind = TyBoolean, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
      Case TkGe Then
        fprintf(cg_out, "  %%r%d = icmp sge ", r)
        CgEmitLlvmType(unified)
        fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
        outTy = TypeInfo With ( kind = TyBoolean, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    End Match
    Return r
  End If

  If k = NdSizeOf Then
    Dim ty As TypeInfo = CgResolveType(g_nodes(id).tyKind, 0, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = add i32 0, %d\n", r, CgTypeSize(ty))
    outTy = TypeInfo With ( kind = TyInt, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    Return r
  End If

  If k = NdCast Then
    Dim srcTy As TypeInfo
    Dim srcReg As Integer = CgEmitExpr(g_nodes(id).left, ByRef srcTy)
    Dim dstKind As Integer = g_nodes(id).tyKind
    Dim dstTy As TypeInfo With ( kind = dstKind, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    outTy = dstTy

    If (srcTy.arrLen = 0) And (srcTy.kind = dstKind) Then
      Return srcReg
    End If

    If dstKind = TyBoolean Then
      Dim r As Integer = CgNextReg()
      If CgIsFloat(srcTy.kind) Then
        fprintf(cg_out, "  %%r%d = fcmp one ", r)
        CgEmitLlvmType(srcTy)
        fprintf(cg_out, " %%r%d, 0.0\n", srcReg)
        Return r
      End If
      If (srcTy.kind = TyPointer) Or (srcTy.arrLen <> 0) Then
        fprintf(cg_out, "  %%r%d = icmp ne ptr %%r%d, null\n", r, srcReg)
        Return r
      End If
      Dim sb As Integer = CgIntBits(srcTy.kind)
      If sb > 0 Then
        fprintf(cg_out, "  %%r%d = icmp ne i%d %%r%d, 0\n", r, sb, srcReg)
        Return r
      End If
      Return srcReg
    End If

    If (srcTy.arrLen = 0) And (srcTy.kind = TyBoolean) Then
      Dim r As Integer = CgNextReg()
      If CgIsFloat(dstKind) Then
        fprintf(cg_out, "  %%r%d = uitofp i1 %%r%d to ", r, srcReg)
        CgEmitLlvmType(dstTy)
        fprintf(cg_out, "\n")
        Return r
      End If
      Dim tb As Integer = CgIntBits(dstKind)
      If tb > 0 Then
        fprintf(cg_out, "  %%r%d = zext i1 %%r%d to i%d\n", r, srcReg, tb)
        Return r
      End If
      Return srcReg
    End If

    If (srcTy.kind = TyPointer) Or (srcTy.arrLen <> 0) Then
      If dstKind = TyPointer Then
        Return srcReg
      End If
      Dim tb As Integer = CgIntBits(dstKind)
      If tb > 0 Then
        Dim r As Integer = CgNextReg()
        fprintf(cg_out, "  %%r%d = ptrtoint ptr %%r%d to i%d\n", r, srcReg, tb)
        Return r
      End If
    End If

    Return CgCoerceScalar(srcReg, srcTy, dstTy)
  End If

  If k = NdStructInit Then
    Dim sIdx As Integer = CgFindStruct(g_nodes(id).intVal, g_nodes(id).lenVal)
    Dim sTy As TypeInfo With (
      kind = TyStruct,
      typeIdx = sIdx,
      arrLen = 0,
      namePos = g_nodes(id).intVal,
      nameLen = g_nodes(id).lenVal
    )
    Dim slot As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = alloca ", slot)
    CgEmitLlvmType(sTy)
    fprintf(cg_out, ", align 8\n")
    fprintf(cg_out, "  store ")
    CgEmitLlvmType(sTy)
    fprintf(cg_out, " zeroinitializer, ptr %%r%d, align 8\n", slot)

    Dim st As Integer = g_nodes(id).firstChild
    Dim cnt As Integer = g_nodes(id).childCount
    For i = 0 To cnt - 1 Do
      Dim fi As Integer = g_kids(st + i)
      Dim fTy As TypeInfo
      Dim fIdx As Integer = CgFindStructField(sIdx, g_nodes(fi).intVal, g_nodes(fi).lenVal, ByRef fTy)
      Dim valTy As TypeInfo
      Dim valReg As Integer = CgEmitExpr(g_nodes(fi).left, ByRef valTy)
      Dim fPtr As Integer = CgNextReg()
      fprintf(cg_out, "  %%r%d = getelementptr inbounds ", fPtr)
      CgEmitLlvmType(sTy)
      fprintf(cg_out, ", ptr %%r%d, i32 0, i32 %d\n", slot, fIdx)
      CgEmitStoreTyped(fPtr, fTy, valReg, valTy)
    End For
    outTy = sTy
    Return slot
  End If

  If k = NdArenaAlloc Then
    cg_usesArena = 1
    Dim arenaPtr As Integer = CgEmitByRefArg(g_nodes(id).left)
    Dim elemTy As TypeInfo = CgResolveType(g_nodes(id).tyKind, 0, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    Dim elemSz As Integer = CgTypeSize(elemTy)
    Dim cntTy As TypeInfo
    Dim cntReg As Integer = CgEmitExpr(g_nodes(id).right, ByRef cntTy)
    Dim cnt64 As Integer = CgWidenToI64(cntReg, cntTy)
    Dim bytesReg As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = mul i64 %%r%d, %d\n", bytesReg, cnt64, elemSz)
    Dim resReg As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = call ptr @__vb_arena_alloc(ptr %%r%d, i64 %%r%d)\n", resReg, arenaPtr, bytesReg)
    outTy = TypeInfo With (
      kind = elemTy.kind,
      typeIdx = elemTy.typeIdx,
      arrLen = -1,
      namePos = elemTy.namePos,
      nameLen = elemTy.nameLen
    )
    Return resReg
  End If

  If k = NdHeapAlloc Then
    Dim elemTy As TypeInfo = CgResolveType(g_nodes(id).tyKind, 0, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    Dim elemSz As Integer = CgTypeSize(elemTy)
    Dim cntTy As TypeInfo
    Dim cntReg As Integer = CgEmitExpr(g_nodes(id).left, ByRef cntTy)
    Dim cnt64 As Integer = CgWidenToI64(cntReg, cntTy)
    Dim resReg As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = call ptr @calloc(i64 %%r%d, i64 %d)\n", resReg, cnt64, elemSz)
    outTy = TypeInfo With (
      kind = elemTy.kind,
      typeIdx = elemTy.typeIdx,
      arrLen = -1,
      namePos = elemTy.namePos,
      nameLen = elemTy.nameLen
    )
    Return resReg
  End If

  If k = NdHeapRealloc Then
    Dim pTy As TypeInfo
    Dim pReg As Integer = CgEmitExpr(g_nodes(id).left, ByRef pTy)
    Dim elemTy As TypeInfo = CgResolveType(g_nodes(id).tyKind, 0, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    Dim elemSz As Integer = CgTypeSize(elemTy)
    Dim cntTy As TypeInfo
    Dim cntReg As Integer = CgEmitExpr(g_nodes(id).right, ByRef cntTy)
    Dim cnt64 As Integer = CgWidenToI64(cntReg, cntTy)
    Dim bytesReg As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = mul i64 %%r%d, %d\n", bytesReg, cnt64, elemSz)
    Dim resReg As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = call ptr @realloc(ptr %%r%d, i64 %%r%d)\n", resReg, pReg, bytesReg)
    outTy = TypeInfo With (
      kind = elemTy.kind,
      typeIdx = elemTy.typeIdx,
      arrLen = -1,
      namePos = elemTy.namePos,
      nameLen = elemTy.nameLen
    )
    Return resReg
  End If

  If k = NdVolatileLoad Then
    Dim addrTy As TypeInfo
    Dim addrReg As Integer = CgEmitExpr(g_nodes(id).left, ByRef addrTy)
    Dim ptrTy As TypeInfo With ( kind = TyPointer, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    Dim addrPtr As Integer = CgCoerceScalar(addrReg, addrTy, ptrTy)
    Dim valTy As TypeInfo = CgResolveType(g_nodes(id).tyKind, 0, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    outTy = valTy
    If CgIsAggregate(valTy) Then
      Dim slot As Integer = CgNextReg()
      Dim al As Integer = CgTypeAlign(valTy)
      Dim sz As Integer = CgTypeSize(valTy)
      fprintf(cg_out, "  %%r%d = alloca ", slot)
      CgEmitLlvmType(valTy)
      fprintf(cg_out, ", align %d\n", al)
      fprintf(cg_out, "  call void @llvm.memcpy.p0.p0.i64(ptr align %d %%r%d, ptr align %d %%r%d, i64 %d, i1 true)\n", al, slot, al, addrPtr, sz)
      Return slot
    End If
    Dim r As Integer = CgNextReg()
    Dim align As Integer = CgTypeAlign(valTy)
    fprintf(cg_out, "  %%r%d = load volatile ", r)
    CgEmitLlvmType(valTy)
    fprintf(cg_out, ", ptr %%r%d, align %d\n", addrPtr, align)
    Return r
  End If

  If k = NdVolatileStore Then
    Dim addrTy As TypeInfo
    Dim addrReg As Integer = CgEmitExpr(g_nodes(id).left, ByRef addrTy)
    Dim ptrTy As TypeInfo With ( kind = TyPointer, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    Dim addrPtr As Integer = CgCoerceScalar(addrReg, addrTy, ptrTy)
    Dim targetTy As TypeInfo = CgResolveTypeEx(g_nodes(id).tyKind, g_nodes(id).tyInnerKind, 0, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    Dim vTy As TypeInfo
    Dim vReg As Integer = CgEmitExpr(g_nodes(id).right, ByRef vTy)
    CgEmitStoreTypedEx(addrPtr, targetTy, vReg, vTy, 1)
    outTy = TypeInfo With ( kind = TyNothing, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    Return 0
  End If

  If k = NdAddressOf Then
    Dim tgt As Integer = g_nodes(id).left
    outTy = TypeInfo With ( kind = TyPointer, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    If (tgt >= 0) And (g_nodes(tgt).kind = NdVar) Then
      Dim vp = g_nodes(tgt).intVal, vl = g_nodes(tgt).lenVal As Integer
      If (CgFindLocal(vp, vl) < 0) And (CgFindGlobal(vp, vl) < 0) Then
        Dim fIdx As Integer = CgFindFunc(vp, vl)
        If fIdx >= 0 Then
          Dim fr As Integer = CgNextReg()
          fprintf(cg_out, "  %%r%d = getelementptr inbounds i8, ptr @", fr)
          CgEmitSymName(cg_funcs(fIdx).namePos, cg_funcs(fIdx).nameLen, cg_funcs(fIdx).modId, cg_funcs(fIdx).isPrivate)
          fprintf(cg_out, ", i64 0\n")
          Return fr
        End If
      End If
    End If
    Dim lTy As TypeInfo
    Dim ind = 0, vol = 0 As Integer
    Return CgEmitLValuePtr(tgt, ByRef lTy, ByRef ind, ByRef vol)
  End If

  If k = NdPtrRead Then
    Dim addrTy As TypeInfo
    Dim addrReg As Integer = CgEmitExpr(g_nodes(id).left, ByRef addrTy)
    Dim ptrTy As TypeInfo With ( kind = TyPointer, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    Dim addrPtr As Integer = CgCoerceScalar(addrReg, addrTy, ptrTy)
    Dim valTy As TypeInfo = CgResolveTypeEx(g_nodes(id).tyKind, g_nodes(id).tyInnerKind, 0, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    outTy = valTy
    If CgIsAggregate(valTy) Then
      Dim slot As Integer = CgNextReg()
      Dim al = CgTypeAlign(valTy), sz = CgTypeSize(valTy) As Integer
      fprintf(cg_out, "  %%r%d = alloca ", slot)
      CgEmitLlvmType(valTy)
      fprintf(cg_out, ", align %d\n", al)
      fprintf(cg_out, "  call void @llvm.memcpy.p0.p0.i64(ptr align %d %%r%d, ptr align %d %%r%d, i64 %d, i1 false)\n", al, slot, al, addrPtr, sz)
      Return slot
    End If
    Dim r As Integer = CgNextReg()
    Dim align As Integer = CgTypeAlign(valTy)
    fprintf(cg_out, "  %%r%d = load ", r)
    CgEmitLlvmType(valTy)
    fprintf(cg_out, ", ptr %%r%d, align %d\n", addrPtr, align)
    Return r
  End If

  If k = NdPtrWrite Then
    Dim addrTy As TypeInfo
    Dim addrReg As Integer = CgEmitExpr(g_nodes(id).left, ByRef addrTy)
    Dim ptrTy As TypeInfo With ( kind = TyPointer, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    Dim addrPtr As Integer = CgCoerceScalar(addrReg, addrTy, ptrTy)
    Dim targetTy As TypeInfo = CgResolveTypeEx(g_nodes(id).tyKind, g_nodes(id).tyInnerKind, 0, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    Dim vTy As TypeInfo
    Dim vReg As Integer = CgEmitExpr(g_nodes(id).right, ByRef vTy)
    CgEmitStoreTypedEx(addrPtr, targetTy, vReg, vTy, 0)
    outTy = TypeInfo With ( kind = TyNothing, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    Return 0
  End If

  If k = NdPtrAdd Then
    Dim addrTy, offTy As TypeInfo
    Dim addrReg As Integer = CgEmitExpr(g_nodes(id).left, ByRef addrTy)
    Dim ptrTy As TypeInfo With ( kind = TyPointer, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    Dim addrPtr As Integer = CgCoerceScalar(addrReg, addrTy, ptrTy)
    Dim offReg As Integer = CgEmitExpr(g_nodes(id).right, ByRef offTy)
    Dim off64 As Integer = CgWidenToI64(offReg, offTy)
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = getelementptr i8, ptr %%r%d, i64 %%r%d\n", r, addrPtr, off64)
    outTy = ptrTy
    Return r
  End If

  If k = NdAwait Then
    cg_usesAsync = 1
    cg_usesFree = 1
    Dim taskTy As TypeInfo
    Dim taskReg As Integer = CgEmitExpr(g_nodes(id).left, ByRef taskTy)
    Dim resTy As TypeInfo With (
      kind = taskTy.innerKind,
      typeIdx = taskTy.typeIdx,
      arrLen = 0,
      namePos = taskTy.namePos,
      nameLen = taskTy.nameLen,
      innerKind = 0
    )
    If cg_isFreestanding = 0 Then
      Dim tidPtr = CgNextReg(), tidVal = CgNextReg() As Integer
      fprintf(cg_out, "  %%r%d = getelementptr inbounds { i64, ptr }, ptr %%r%d, i32 0, i32 0\n", tidPtr, taskReg)
      fprintf(cg_out, "  %%r%d = load i64, ptr %%r%d, align 8\n", tidVal, tidPtr)
      fprintf(cg_out, "  call i32 @pthread_join(i64 %%r%d, ptr null)\n", tidVal)
    Else
      Dim thkPtr = CgNextReg(), thkVal = CgNextReg() As Integer
      fprintf(cg_out, "  %%r%d = getelementptr inbounds { i64, ptr }, ptr %%r%d, i32 0, i32 1\n", thkPtr, taskReg)
      fprintf(cg_out, "  %%r%d = load ptr, ptr %%r%d, align %d\n", thkVal, thkPtr, cg_ptrAlign)
      fprintf(cg_out, "  call ptr %%r%d(ptr %%r%d)\n", thkVal, taskReg)
    End If
    outTy = resTy
    If resTy.kind = TyNothing Then
      fprintf(cg_out, "  call void @free(ptr %%r%d)\n", taskReg)
      Return 0
    End If
    Dim resPtr As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = getelementptr inbounds { i64, ptr, ", resPtr)
    CgEmitLlvmType(resTy)
    fprintf(cg_out, " }, ptr %%r%d, i32 0, i32 2\n", taskReg)
    If CgIsAggregate(resTy) Then
      Dim slot = CgNextReg(), al = CgTypeAlign(resTy), sz = CgTypeSize(resTy) As Integer
      fprintf(cg_out, "  %%r%d = alloca ", slot)
      CgEmitLlvmType(resTy)
      fprintf(cg_out, ", align %d\n", al)
      fprintf(cg_out, "  call void @llvm.memcpy.p0.p0.i64(ptr align %d %%r%d, ptr align %d %%r%d, i64 %d, i1 false)\n", al, slot, al, resPtr, sz)
      fprintf(cg_out, "  call void @free(ptr %%r%d)\n", taskReg)
      Return slot
    End If
    Dim resVal = CgNextReg(), align = CgTypeAlign(resTy) As Integer
    fprintf(cg_out, "  %%r%d = load ", resVal)
    CgEmitLlvmType(resTy)
    fprintf(cg_out, ", ptr %%r%d, align %d\n", resPtr, align)
    fprintf(cg_out, "  call void @free(ptr %%r%d)\n", taskReg)
    Return resVal
  End If

  If k = NdCall Then
    Return CgEmitCall(id, ByRef outTy)
  End If

  outTy = TypeInfo With ( kind = TyInt, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
  Return 0
End Function

Private Function CgEmitCall(id As Integer, ByRef outTy As TypeInfo) As Integer
  Dim nPos As Integer = g_nodes(id).intVal
  Dim nLen As Integer = g_nodes(id).lenVal
  Dim aStart As Integer = g_nodes(id).firstChild
  Dim aCount As Integer = g_nodes(id).childCount

  ' Builtin: IsNull(p)
  If SliceEqualsStr(nPos, nLen, "IsNull", 6) Then
    Dim a0 As Integer = g_nodes(g_kids(aStart)).left
    Dim pt As TypeInfo
    Dim pv As Integer = CgEmitExpr(a0, ByRef pt)
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = icmp eq ptr %%r%d, null\n", r, pv)
    outTy = TypeInfo With ( kind = TyBoolean, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    Return r
  End If

  ' Builtin: Len(arr)
  If SliceEqualsStr(nPos, nLen, "Len", 3) Then
    Dim a0 As Integer = g_nodes(g_kids(aStart)).left
    Dim aty As TypeInfo
    Dim ind = 0, vol = 0 As Integer
    CgEmitLValuePtr(a0, ByRef aty, ByRef ind, ByRef vol)
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = add i32 0, %d\n", r, aty.arrLen)
    outTy = TypeInfo With ( kind = TyInt, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    Return r
  End If

  ' Builtin: ArenaCreate(cap)
  If SliceEqualsStr(nPos, nLen, "ArenaCreate", 11) Then
    cg_usesArena = 1
    Dim capTy As TypeInfo
    Dim capReg As Integer = CgEmitExpr(g_nodes(g_kids(aStart)).left, ByRef capTy)
    Dim cap64 As Integer = CgWidenToI64(capReg, capTy)
    Dim tmp As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = alloca %%struct.__vb_arena, align 8\n", tmp)
    fprintf(cg_out, "  call void @__vb_arena_init_heap(ptr %%r%d, i64 %%r%d)\n", tmp, cap64)
    outTy = TypeInfo With ( kind = TyArena, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    Return tmp
  End If

  ' Builtin: ArenaFromBuffer(ByRef buf, cap)
  If SliceEqualsStr(nPos, nLen, "ArenaFromBuffer", 15) Then
    cg_usesArena = 1
    Dim bufPtr As Integer = CgEmitByRefArg(g_nodes(g_kids(aStart)).left)
    Dim capTy As TypeInfo
    Dim capReg As Integer = CgEmitExpr(g_nodes(g_kids(aStart + 1)).left, ByRef capTy)
    Dim cap64 As Integer = CgWidenToI64(capReg, capTy)
    Dim tmp As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = alloca %%struct.__vb_arena, align 8\n", tmp)
    fprintf(cg_out, "  call void @__vb_arena_init_buf(ptr %%r%d, ptr %%r%d, i64 %%r%d)\n", tmp, bufPtr, cap64)
    outTy = TypeInfo With ( kind = TyArena, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    Return tmp
  End If

  ' Builtin: ArenaAllocBytes(ByRef a, bytes)
  If SliceEqualsStr(nPos, nLen, "ArenaAllocBytes", 15) Then
    cg_usesArena = 1
    Dim aPtr As Integer = CgEmitByRefArg(g_nodes(g_kids(aStart)).left)
    Dim bTy As TypeInfo
    Dim bReg As Integer = CgEmitExpr(g_nodes(g_kids(aStart + 1)).left, ByRef bTy)
    Dim b64 As Integer = CgWidenToI64(bReg, bTy)
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = call ptr @__vb_arena_alloc(ptr %%r%d, i64 %%r%d)\n", r, aPtr, b64)
    outTy = TypeInfo With ( kind = TyPointer, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    Return r
  End If

  ' Builtin: ArenaReset(ByRef a)
  If SliceEqualsStr(nPos, nLen, "ArenaReset", 10) Then
    cg_usesArena = 1
    Dim aPtr As Integer = CgEmitByRefArg(g_nodes(g_kids(aStart)).left)
    fprintf(cg_out, "  call void @__vb_arena_reset(ptr %%r%d)\n", aPtr)
    outTy = TypeInfo With ( kind = TyNothing, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    Return 0
  End If

  ' Builtin: ArenaDestroy(ByRef a)
  If SliceEqualsStr(nPos, nLen, "ArenaDestroy", 12) Then
    cg_usesArena = 1
    Dim aPtr As Integer = CgEmitByRefArg(g_nodes(g_kids(aStart)).left)
    fprintf(cg_out, "  call void @__vb_arena_destroy(ptr %%r%d)\n", aPtr)
    outTy = TypeInfo With ( kind = TyNothing, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    Return 0
  End If

  ' Builtins: ArenaUsed / ArenaMark / ArenaRemaining / ArenaCapacity
  If SliceEqualsStr(nPos, nLen, "ArenaUsed", 9) Or SliceEqualsStr(nPos, nLen, "ArenaMark", 9) Then
    cg_usesArena = 1
    Dim aPtr As Integer = CgEmitByRefArg(g_nodes(g_kids(aStart)).left)
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = call i64 @__vb_arena_used(ptr %%r%d)\n", r, aPtr)
    outTy = TypeInfo With ( kind = TyLong, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    Return r
  End If

  If SliceEqualsStr(nPos, nLen, "ArenaRemaining", 14) Then
    cg_usesArena = 1
    Dim aPtr As Integer = CgEmitByRefArg(g_nodes(g_kids(aStart)).left)
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = call i64 @__vb_arena_remaining(ptr %%r%d)\n", r, aPtr)
    outTy = TypeInfo With ( kind = TyLong, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    Return r
  End If

  If SliceEqualsStr(nPos, nLen, "ArenaCapacity", 13) Then
    cg_usesArena = 1
    Dim aPtr As Integer = CgEmitByRefArg(g_nodes(g_kids(aStart)).left)
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = call i64 @__vb_arena_capacity(ptr %%r%d)\n", r, aPtr)
    outTy = TypeInfo With ( kind = TyLong, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    Return r
  End If

  If SliceEqualsStr(nPos, nLen, "ArenaRewind", 11) Then
    cg_usesArena = 1
    Dim aPtr As Integer = CgEmitByRefArg(g_nodes(g_kids(aStart)).left)
    Dim mTy As TypeInfo
    Dim mReg As Integer = CgEmitExpr(g_nodes(g_kids(aStart + 1)).left, ByRef mTy)
    Dim m64 As Integer = CgWidenToI64(mReg, mTy)
    fprintf(cg_out, "  call void @__vb_arena_rewind(ptr %%r%d, i64 %%r%d)\n", aPtr, m64)
    outTy = TypeInfo With ( kind = TyNothing, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    Return 0
  End If

  ' Builtin: HeapAllocBytes(bytes)
  If SliceEqualsStr(nPos, nLen, "HeapAllocBytes", 14) Then
    Dim bTy As TypeInfo
    Dim bReg As Integer = CgEmitExpr(g_nodes(g_kids(aStart)).left, ByRef bTy)
    Dim b64 As Integer = CgWidenToI64(bReg, bTy)
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = call ptr @calloc(i64 1, i64 %%r%d)\n", r, b64)
    outTy = TypeInfo With ( kind = TyPointer, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    Return r
  End If

  ' Builtin: HeapFree(p)
  If SliceEqualsStr(nPos, nLen, "HeapFree", 8) Then
    Dim pTy As TypeInfo
    Dim pReg As Integer = CgEmitExpr(g_nodes(g_kids(aStart)).left, ByRef pTy)
    fprintf(cg_out, "  call void @free(ptr %%r%d)\n", pReg)
    outTy = TypeInfo With ( kind = TyNothing, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    Return 0
  End If

  ' Builtin: Printf
  If SliceEqualsStr(nPos, nLen, "Printf", 6) Then
    Dim argRegs(32) As Integer
    Dim argTys(32) As TypeInfo
    Dim i As Integer = 0
    While i < aCount Do
      Dim aNode As Integer = g_kids(aStart + i)
      Dim aTy As TypeInfo
      Dim aReg As Integer = CgEmitExpr(g_nodes(aNode).left, ByRef aTy)
      If i > 0 Then
        aReg = CgPromoteVariadic(aReg, ByRef aTy)
      End If
      argRegs(i) = aReg
      argTys(i) = aTy
      i = i + 1
    End While
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = call i32 (ptr, ...) @printf(", r)
    Dim j As Integer = 0
    While j < aCount Do
      If j > 0 Then
        fprintf(cg_out, ", ")
      End If
      If CgIsAggregate(argTys(j)) Or (argTys(j).arrLen < 0) Then
        fprintf(cg_out, "ptr %%r%d", argRegs(j))
      Else
        CgEmitLlvmType(argTys(j))
        fprintf(cg_out, " %%r%d", argRegs(j))
      End If
      j = j + 1
    End While
    fprintf(cg_out, ")\n")
    outTy = TypeInfo With ( kind = TyInt, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    Return r
  End If

  ' Builtin: Calloc(n, sz)
  If SliceEqualsStr(nPos, nLen, "Calloc", 6) Then
    Dim nTy As TypeInfo
    Dim nReg As Integer = CgEmitExpr(g_nodes(g_kids(aStart)).left, ByRef nTy)
    Dim n64 As Integer = CgWidenToI64(nReg, nTy)
    Dim szTy As TypeInfo
    Dim szReg As Integer = CgEmitExpr(g_nodes(g_kids(aStart + 1)).left, ByRef szTy)
    Dim sz64 As Integer = CgWidenToI64(szReg, szTy)
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = call ptr @calloc(i64 %%r%d, i64 %%r%d)\n", r, n64, sz64)
    outTy = TypeInfo With ( kind = TyPointer, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    Return r
  End If

  ' Builtin: Realloc(p, sz)
  If SliceEqualsStr(nPos, nLen, "Realloc", 7) Then
    Dim pTy As TypeInfo
    Dim pReg As Integer = CgEmitExpr(g_nodes(g_kids(aStart)).left, ByRef pTy)
    Dim szTy As TypeInfo
    Dim szReg As Integer = CgEmitExpr(g_nodes(g_kids(aStart + 1)).left, ByRef szTy)
    Dim sz64 As Integer = CgWidenToI64(szReg, szTy)
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = call ptr @realloc(ptr %%r%d, i64 %%r%d)\n", r, pReg, sz64)
    outTy = TypeInfo With ( kind = TyPointer, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    Return r
  End If

  ' Positional struct constructor: StructName(arg0, arg1, ...)
  Dim ctorStructIdx As Integer = CgFindStruct(nPos, nLen)
  If ctorStructIdx >= 0 Then
    Dim sTy As TypeInfo With (
      kind = TyStruct,
      typeIdx = ctorStructIdx,
      arrLen = 0,
      namePos = nPos,
      nameLen = nLen
    )
    Dim slot As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = alloca ", slot)
    CgEmitLlvmType(sTy)
    fprintf(cg_out, ", align 8\n")
    fprintf(cg_out, "  store ")
    CgEmitLlvmType(sTy)
    fprintf(cg_out, " zeroinitializer, ptr %%r%d, align 8\n", slot)
    Dim fst As Integer = cg_structs(ctorStructIdx).fieldStart
    For ci = 0 To aCount - 1 Do
      Dim fTy As TypeInfo = cg_fields(fst + ci).ty
      Dim aNode As Integer = g_kids(aStart + ci)
      Dim valTy As TypeInfo
      Dim valReg As Integer = CgEmitExpr(g_nodes(aNode).left, ByRef valTy)
      Dim fPtr As Integer = CgNextReg()
      fprintf(cg_out, "  %%r%d = getelementptr inbounds ", fPtr)
      CgEmitLlvmType(sTy)
      fprintf(cg_out, ", ptr %%r%d, i32 0, i32 %d\n", slot, ci)
      CgEmitStoreTyped(fPtr, fTy, valReg, valTy)
    End For
    outTy = sTy
    Return slot
  End If

  ' User Function / Sub / ExternDecl / C runtime builtin
  Dim fnIdx As Integer = CgFindFunc(nPos, nLen)
  Dim isExternFn As Boolean = (fnIdx >= 0) And (cg_funcs(fnIdx).isExtern = 1)
  Dim argRegs(64) As Integer
  Dim argTys(64) As TypeInfo
  Dim argIsPtr(64) As Integer
  Dim argCAbi(64) As Integer
  Dim argCStruct(64) As Integer
  Dim emitArgCnt As Integer = 0

  For i = 0 To aCount - 1 Do
    Dim aNode As Integer = g_kids(aStart + i)
    Dim isByRef As Integer = g_nodes(aNode).op
    Dim exprId As Integer = g_nodes(aNode).left
    If isByRef = 1 Then
      argRegs(emitArgCnt) = CgEmitByRefArg(exprId)
      argIsPtr(emitArgCnt) = 1
      argCAbi(emitArgCnt) = 0
      argCStruct(emitArgCnt) = -1
      argTys(emitArgCnt) = TypeInfo With ( kind = TyPointer, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
      emitArgCnt = emitArgCnt + 1
    Else
      Dim aTy As TypeInfo
      Dim aReg As Integer = CgEmitExpr(exprId, ByRef aTy)
      If (fnIdx >= 0) And (i < cg_funcs(fnIdx).paramCount) Then
        Dim pInfo As CgParamInfo = cg_params(cg_funcs(fnIdx).paramStart + i)
        If isExternFn And (pInfo.isByRef = 0) And (pInfo.ty.kind = TyStruct) And (pInfo.ty.arrLen = 0) And (pInfo.ty.typeIdx >= 0) Then
          Dim t0 As Integer = 0
          Dim t1 As Integer = 0
          Dim mode As Integer = CgClassifyCStruct(pInfo.ty.typeIdx, ByRef t0, ByRef t1)
          If mode = 1 Then
            Dim r0 As Integer = CgNextReg()
            fprintf(cg_out, "  %%r%d = load ", r0)
            CgEmitCAbiCode(t0, pInfo.ty.typeIdx)
            fprintf(cg_out, ", ptr %%r%d, align 4\n", aReg)
            argRegs(emitArgCnt) = r0
            argIsPtr(emitArgCnt) = 0
            argCAbi(emitArgCnt) = t0
            argCStruct(emitArgCnt) = pInfo.ty.typeIdx
            emitArgCnt = emitArgCnt + 1
          Else
            If mode = 2 Then
              Dim r0 As Integer = CgNextReg()
              fprintf(cg_out, "  %%r%d = load ", r0)
              CgEmitCAbiCode(t0, pInfo.ty.typeIdx)
              fprintf(cg_out, ", ptr %%r%d, align 4\n", aReg)
              Dim p1 As Integer = CgNextReg()
              fprintf(cg_out, "  %%r%d = getelementptr inbounds i8, ptr %%r%d, i64 8\n", p1, aReg)
              Dim r1 As Integer = CgNextReg()
              fprintf(cg_out, "  %%r%d = load ", r1)
              CgEmitCAbiCode(t1, pInfo.ty.typeIdx)
              fprintf(cg_out, ", ptr %%r%d, align 4\n", p1)
              argRegs(emitArgCnt) = r0
              argIsPtr(emitArgCnt) = 0
              argCAbi(emitArgCnt) = t0
              argCStruct(emitArgCnt) = pInfo.ty.typeIdx
              emitArgCnt = emitArgCnt + 1
              argRegs(emitArgCnt) = r1
              argIsPtr(emitArgCnt) = 0
              argCAbi(emitArgCnt) = t1
              argCStruct(emitArgCnt) = pInfo.ty.typeIdx
              emitArgCnt = emitArgCnt + 1
            Else
              If mode = 3 Then
                Dim r0 As Integer = CgNextReg()
                Dim al As Integer = CgTypeAlign(pInfo.ty)
                fprintf(cg_out, "  %%r%d = load ", r0)
                CgEmitLlvmType(pInfo.ty)
                fprintf(cg_out, ", ptr %%r%d, align %d\n", aReg, al)
                argRegs(emitArgCnt) = r0
                argIsPtr(emitArgCnt) = 0
                argCAbi(emitArgCnt) = 0
                argCStruct(emitArgCnt) = -1
                argTys(emitArgCnt) = pInfo.ty
                emitArgCnt = emitArgCnt + 1
              Else
                If mode = 4 Then
                  Dim al As Integer = CgTypeAlign(pInfo.ty)
                  Dim cpPtr As Integer = CgNextReg()
                  fprintf(cg_out, "  %%r%d = alloca ", cpPtr)
                  CgEmitLlvmType(pInfo.ty)
                  fprintf(cg_out, ", align %d\n", al)
                  Dim cpVal As Integer = CgNextReg()
                  fprintf(cg_out, "  %%r%d = load ", cpVal)
                  CgEmitLlvmType(pInfo.ty)
                  fprintf(cg_out, ", ptr %%r%d, align %d\n", aReg, al)
                  fprintf(cg_out, "  store ")
                  CgEmitLlvmType(pInfo.ty)
                  fprintf(cg_out, " %%r%d, ptr %%r%d, align %d\n", cpVal, cpPtr, al)
                  argRegs(emitArgCnt) = cpPtr
                  argIsPtr(emitArgCnt) = 0
                  argCAbi(emitArgCnt) = 9
                  argCStruct(emitArgCnt) = pInfo.ty.typeIdx
                  emitArgCnt = emitArgCnt + 1
                Else
                  argRegs(emitArgCnt) = aReg
                  argIsPtr(emitArgCnt) = 0
                  argCAbi(emitArgCnt) = 8
                  argCStruct(emitArgCnt) = pInfo.ty.typeIdx
                  emitArgCnt = emitArgCnt + 1
                End If
              End If
            End If
          End If
        Else
          If (pInfo.ty.arrLen <> 0) Or (pInfo.ty.kind = TyStruct) Or (pInfo.ty.kind = TyArena) Then
            argRegs(emitArgCnt) = aReg
            argIsPtr(emitArgCnt) = 1
            argCAbi(emitArgCnt) = 0
            argCStruct(emitArgCnt) = -1
            argTys(emitArgCnt) = pInfo.ty
            emitArgCnt = emitArgCnt + 1
          Else
            argRegs(emitArgCnt) = CgCoerceScalar(aReg, aTy, pInfo.ty)
            argIsPtr(emitArgCnt) = 0
            argCAbi(emitArgCnt) = 0
            argCStruct(emitArgCnt) = -1
            argTys(emitArgCnt) = pInfo.ty
            emitArgCnt = emitArgCnt + 1
          End If
        End If
      Else
        If (fnIdx >= 0) And (cg_funcs(fnIdx).isVariadic = 1) Then
          aReg = CgPromoteVariadic(aReg, ByRef aTy)
        End If
        argRegs(emitArgCnt) = aReg
        If CgIsAggregate(aTy) Or (aTy.arrLen < 0) Then
          argIsPtr(emitArgCnt) = 1
        Else
          argIsPtr(emitArgCnt) = 0
        End If
        argCAbi(emitArgCnt) = 0
        argCStruct(emitArgCnt) = -1
        argTys(emitArgCnt) = aTy
        emitArgCnt = emitArgCnt + 1
      End If
    End If
  End For

  Dim retTy As TypeInfo With ( kind = TyInt, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
  Dim isVar As Integer = 0
  Dim cBuiltin As Integer = 0
  If fnIdx >= 0 Then
    retTy = cg_funcs(fnIdx).retTy
    isVar = cg_funcs(fnIdx).isVariadic
  Else
    Match With
      Case SliceEqualsStr(nPos, nLen, "Fopen", 5) Then
        retTy.kind = TyPointer
        cBuiltin = 1
      Case SliceEqualsStr(nPos, nLen, "Malloc", 6) Then
        retTy.kind = TyPointer
        cBuiltin = 2
      Case SliceEqualsStr(nPos, nLen, "Free", 4) Then
        retTy.kind = TyNothing
        cBuiltin = 3
      Case SliceEqualsStr(nPos, nLen, "Getc", 4) Then cBuiltin = 4
      Case SliceEqualsStr(nPos, nLen, "Putc", 4) Then cBuiltin = 5
    End Match
  End If

  Dim resReg As Integer = 0
  If retTy.kind = TyNothing Then
    fprintf(cg_out, "  call void")
  Else
    resReg = CgNextReg()
    fprintf(cg_out, "  %%r%d = call ", resReg)
    If isExternFn Then
      CgEmitExternRetType(retTy)
    Else
      CgEmitLlvmType(retTy)
    End If
  End If

  If (fnIdx >= 0) And (isVar = 1) Then
    fprintf(cg_out, " (")
    For pi = 0 To cg_funcs(fnIdx).paramCount - 1 Do
      If pi > 0 Then
        fprintf(cg_out, ", ")
      End If
      If isExternFn Then
        CgEmitExternParamType(cg_params(cg_funcs(fnIdx).paramStart + pi))
      Else
        CgEmitParamLlvmType(cg_params(cg_funcs(fnIdx).paramStart + pi))
      End If
    End For
    If cg_funcs(fnIdx).paramCount > 0 Then
      fprintf(cg_out, ", ...")
    Else
      fprintf(cg_out, "...")
    End If
    fprintf(cg_out, ")")
  End If

  fprintf(cg_out, " @")
  If fnIdx >= 0 Then
    CgEmitSymName(nPos, nLen, cg_funcs(fnIdx).modId, cg_funcs(fnIdx).isPrivate)
  Else
    Match cBuiltin With
      Case 1 Then fprintf(cg_out, "fopen")
      Case 2 Then fprintf(cg_out, "malloc")
      Case 3 Then fprintf(cg_out, "free")
      Case 4 Then fprintf(cg_out, "getc")
      Case 5 Then fprintf(cg_out, "putc")
      Case _ Then CgEmitSlice(nPos, nLen)
    End Match
  End If
  fprintf(cg_out, "(")
  For j = 0 To emitArgCnt - 1 Do
    If j > 0 Then
      fprintf(cg_out, ", ")
    End If
    If argCAbi(j) > 0 Then
      CgEmitCAbiCode(argCAbi(j), argCStruct(j))
      fprintf(cg_out, " %%r%d", argRegs(j))
    Else
      If argIsPtr(j) = 1 Then
        fprintf(cg_out, "ptr %%r%d", argRegs(j))
      Else
        CgEmitLlvmType(argTys(j))
        fprintf(cg_out, " %%r%d", argRegs(j))
      End If
    End If
  End For
  fprintf(cg_out, ")\n")

  outTy = retTy
  If CgIsAggregate(retTy) Then
    Dim tmp As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = alloca ", tmp)
    CgEmitLlvmType(retTy)
    fprintf(cg_out, ", align 8\n")
    If isExternFn And (retTy.kind = TyStruct) And (retTy.arrLen = 0) And (retTy.typeIdx >= 0) Then
      Dim rt0 As Integer = 0
      Dim rt1 As Integer = 0
      Dim rmode As Integer = CgClassifyCStruct(retTy.typeIdx, ByRef rt0, ByRef rt1)
      If rmode = 1 Then
        fprintf(cg_out, "  store ")
        CgEmitCAbiCode(rt0, retTy.typeIdx)
        fprintf(cg_out, " %%r%d, ptr %%r%d, align 4\n", resReg, tmp)
        Return tmp
      End If
      If rmode = 2 Then
        Dim e0 As Integer = CgNextReg()
        fprintf(cg_out, "  %%r%d = extractvalue ", e0)
        CgEmitExternRetType(retTy)
        fprintf(cg_out, " %%r%d, 0\n", resReg)
        Dim e1 As Integer = CgNextReg()
        fprintf(cg_out, "  %%r%d = extractvalue ", e1)
        CgEmitExternRetType(retTy)
        fprintf(cg_out, " %%r%d, 1\n", resReg)
        fprintf(cg_out, "  store ")
        CgEmitCAbiCode(rt0, retTy.typeIdx)
        fprintf(cg_out, " %%r%d, ptr %%r%d, align 4\n", e0, tmp)
        Dim p1 As Integer = CgNextReg()
        fprintf(cg_out, "  %%r%d = getelementptr inbounds i8, ptr %%r%d, i64 8\n", p1, tmp)
        fprintf(cg_out, "  store ")
        CgEmitCAbiCode(rt1, retTy.typeIdx)
        fprintf(cg_out, " %%r%d, ptr %%r%d, align 4\n", e1, p1)
        Return tmp
      End If
    End If
    fprintf(cg_out, "  store ")
    CgEmitLlvmType(retTy)
    fprintf(cg_out, " %%r%d, ptr %%r%d, align 8\n", resReg, tmp)
    Return tmp
  End If
  Return resReg
End Function

Private Function CgEmitOpCall1(opIdx As Integer, v1 As Integer, t1 As TypeInfo, ByRef outTy As TypeInfo) As Integer
  Dim oi As CgOpInfo = cg_ops(opIdx)
  Dim p0 As CgParamInfo = cg_params(oi.paramStart)
  Dim a0Reg As Integer = v1
  Dim a0IsPtr As Integer = 0
  If (p0.ty.arrLen <> 0) Or (p0.ty.kind = TyStruct) Or (p0.ty.kind = TyArena) Then
    a0IsPtr = 1
  Else
    a0Reg = CgCoerceScalar(v1, t1, p0.ty)
  End If
  Dim resReg As Integer = CgNextReg()
  fprintf(cg_out, "  %%r%d = call ", resReg)
  CgEmitLlvmType(oi.retTy)
  fprintf(cg_out, " @")
  CgEmitOpSymName(opIdx)
  fprintf(cg_out, "(")
  If a0IsPtr = 1 Then
    fprintf(cg_out, "ptr %%r%d)\n", a0Reg)
  Else
    CgEmitLlvmType(p0.ty)
    fprintf(cg_out, " %%r%d)\n", a0Reg)
  End If
  outTy = oi.retTy
  If CgIsAggregate(oi.retTy) Then
    Dim tmp As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = alloca ", tmp)
    CgEmitLlvmType(oi.retTy)
    fprintf(cg_out, ", align 8\n")
    fprintf(cg_out, "  store ")
    CgEmitLlvmType(oi.retTy)
    fprintf(cg_out, " %%r%d, ptr %%r%d, align 8\n", resReg, tmp)
    Return tmp
  End If
  Return resReg
End Function

Private Function CgEmitOpCall2(opIdx As Integer, v1 As Integer, t1 As TypeInfo, v2 As Integer, t2 As TypeInfo, ByRef outTy As TypeInfo) As Integer
  Dim oi As CgOpInfo = cg_ops(opIdx)
  Dim p0 As CgParamInfo = cg_params(oi.paramStart)
  Dim p1 As CgParamInfo = cg_params(oi.paramStart + 1)
  Dim a0Reg = v1, a1Reg = v2, a0IsPtr = 0, a1IsPtr = 0 As Integer
  If (p0.ty.arrLen <> 0) Or (p0.ty.kind = TyStruct) Or (p0.ty.kind = TyArena) Then
    a0IsPtr = 1
  Else
    a0Reg = CgCoerceScalar(v1, t1, p0.ty)
  End If
  If (p1.ty.arrLen <> 0) Or (p1.ty.kind = TyStruct) Or (p1.ty.kind = TyArena) Then
    a1IsPtr = 1
  Else
    a1Reg = CgCoerceScalar(v2, t2, p1.ty)
  End If
  Dim resReg As Integer = CgNextReg()
  fprintf(cg_out, "  %%r%d = call ", resReg)
  CgEmitLlvmType(oi.retTy)
  fprintf(cg_out, " @")
  CgEmitOpSymName(opIdx)
  fprintf(cg_out, "(")
  If a0IsPtr = 1 Then
    fprintf(cg_out, "ptr %%r%d, ", a0Reg)
  Else
    CgEmitLlvmType(p0.ty)
    fprintf(cg_out, " %%r%d, ", a0Reg)
  End If
  If a1IsPtr = 1 Then
    fprintf(cg_out, "ptr %%r%d)\n", a1Reg)
  Else
    CgEmitLlvmType(p1.ty)
    fprintf(cg_out, " %%r%d)\n", a1Reg)
  End If
  outTy = oi.retTy
  If CgIsAggregate(oi.retTy) Then
    Dim tmp As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = alloca ", tmp)
    CgEmitLlvmType(oi.retTy)
    fprintf(cg_out, ", align 8\n")
    fprintf(cg_out, "  store ")
    CgEmitLlvmType(oi.retTy)
    fprintf(cg_out, " %%r%d, ptr %%r%d, align 8\n", resReg, tmp)
    Return tmp
  End If
  Return resReg
End Function

Private Function CgBlockTerminates(startIdx As Integer, cnt As Integer) As Boolean
  For i = 0 To cnt - 1 Do
    Dim sid As Integer = g_kids(startIdx + i)
    If g_nodes(sid).kind = NdReturn Then
      Return True
    End If
  End For
  Return False
End Function

Private Sub CgEmitStmt(id As Integer)
  Dim k As Integer = g_nodes(id).kind

  If cg_blockEnded = 1 Then
    If k = NdLabel Then
      fprintf(cg_out, "ulbl_")
      CgEmitSlice(g_nodes(id).intVal, g_nodes(id).lenVal)
      fprintf(cg_out, ":\n")
      cg_blockEnded = 0
    End If
    Return
  End If

  If k = NdLabel Then
    fprintf(cg_out, "  br label %%ulbl_")
    CgEmitSlice(g_nodes(id).intVal, g_nodes(id).lenVal)
    fprintf(cg_out, "\nulbl_")
    CgEmitSlice(g_nodes(id).intVal, g_nodes(id).lenVal)
    fprintf(cg_out, ":\n")
    Return
  End If

  If k = NdGoTo Then
    fprintf(cg_out, "  br label %%ulbl_")
    CgEmitSlice(g_nodes(id).intVal, g_nodes(id).lenVal)
    fprintf(cg_out, "\n")
    cg_blockEnded = 1
    Return
  End If

  If k = NdBreak Then
    If cg_loopDepth > 0 Then
      fprintf(cg_out, "  br label %%L%d\n", cg_loopBreakLbls(cg_loopDepth - 1))
      cg_blockEnded = 1
    End If
    Return
  End If

  If k = NdContinue Then
    If cg_loopDepth > 0 Then
      fprintf(cg_out, "  br label %%L%d\n", cg_loopContLbls(cg_loopDepth - 1))
      cg_blockEnded = 1
    End If
    Return
  End If

  If k = NdStaticDim Then
    Dim sid As Integer = g_nodes(id).op
    Dim ty As TypeInfo = cg_staticLocals(sid).ty
    Dim isInd As Integer = 0
    If ty.arrLen < 0 Then
      isInd = 1
    End If
    cg_locals(cg_localCount) = CgLocalSym With (
      namePos = g_nodes(id).intVal,
      nameLen = g_nodes(id).lenVal,
      slotReg = 0,
      isIndirect = isInd,
      staticPlusOne = sid + 1,
      isVolatile = cg_staticLocals(sid).isVolatile,
      ty = ty
    )
    cg_localCount = cg_localCount + 1
    Return
  End If

  If k = NdDim Then
    Dim ty As TypeInfo = CgResolveTypeEx(g_nodes(id).tyKind, g_nodes(id).tyInnerKind, g_nodes(id).tyArrLen, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    Dim slot As Integer = CgNextReg()
    Dim isInd As Integer = 0
    If ty.arrLen < 0 Then
      isInd = 1
    End If
    Dim isVol As Integer = 0
    If g_nodes(id).flags = 1 Then
      isVol = 1
    End If
    fprintf(cg_out, "  %%r%d = alloca ", slot)
    CgEmitLlvmType(ty)
    fprintf(cg_out, ", align 8\n")

    cg_locals(cg_localCount) = CgLocalSym With (
      namePos = g_nodes(id).intVal,
      nameLen = g_nodes(id).lenVal,
      slotReg = slot,
      isIndirect = isInd,
      isVolatile = isVol,
      ty = ty
    )
    cg_localCount = cg_localCount + 1

    Dim initId As Integer = g_nodes(id).left
    If initId < 0 Then
      Dim align As Integer = CgTypeAlign(ty)
      If isVol = 1 Then
        fprintf(cg_out, "  store volatile ")
      Else
        fprintf(cg_out, "  store ")
      End If
      CgEmitLlvmType(ty)
      fprintf(cg_out, " ")
      CgEmitZeroVal(ty)
      fprintf(cg_out, ", ptr %%r%d, align %d\n", slot, align)
      Return
    End If

    If (ty.arrLen > 0) And (g_nodes(initId).kind = NdArrayLit) Then
      fprintf(cg_out, "  store ")
      CgEmitLlvmType(ty)
      fprintf(cg_out, " zeroinitializer, ptr %%r%d, align 8\n", slot)
      Dim elemTy As TypeInfo = CgElemType(ty)
      Dim st As Integer = g_nodes(initId).firstChild
      Dim cnt As Integer = g_nodes(initId).childCount
      Dim i As Integer = 0
      While i < cnt Do
        Dim vTy As TypeInfo
        Dim vReg As Integer = CgEmitExpr(g_kids(st + i), ByRef vTy)
        Dim ePtr As Integer = CgNextReg()
        fprintf(cg_out, "  %%r%d = getelementptr inbounds ", ePtr)
        CgEmitLlvmType(ty)
        fprintf(cg_out, ", ptr %%r%d, i64 0, i64 %d\n", slot, i)
        CgEmitStoreTypedEx(ePtr, elemTy, vReg, vTy, isVol)
        i = i + 1
      End While
      Return
    End If

    Dim vTy As TypeInfo
    Dim vReg As Integer = CgEmitExpr(initId, ByRef vTy)
    CgEmitStoreTypedEx(slot, ty, vReg, vTy, isVol)
    Return
  End If

  If k = NdAssign Then
    Dim tmpVar As Integer = NewNode(NdVar)
    g_nodes(tmpVar).intVal = g_nodes(id).intVal
    g_nodes(tmpVar).lenVal = g_nodes(id).lenVal
    Dim dstTy As TypeInfo
    Dim isInd = 0, isVol = 0 As Integer
    Dim dstPtr As Integer = CgEmitLValuePtr(tmpVar, ByRef dstTy, ByRef isInd, ByRef isVol)
    Dim vTy As TypeInfo
    Dim vReg As Integer = CgEmitExpr(g_nodes(id).left, ByRef vTy)
    CgEmitStoreTypedEx(dstPtr, dstTy, vReg, vTy, isVol)
    Return
  End If

  If k = NdFieldAssign Then
    Dim tmpFa As Integer = NewNode(NdFieldAccess)
    g_nodes(tmpFa).left = g_nodes(id).left
    g_nodes(tmpFa).intVal = g_nodes(id).intVal
    g_nodes(tmpFa).lenVal = g_nodes(id).lenVal
    Dim vTy As TypeInfo
    Dim vReg As Integer = CgEmitExpr(g_nodes(id).right, ByRef vTy)
    Dim dstTy As TypeInfo
    Dim isInd = 0, isVol = 0 As Integer
    Dim dstPtr As Integer = CgEmitLValuePtr(tmpFa, ByRef dstTy, ByRef isInd, ByRef isVol)
    CgEmitStoreTypedEx(dstPtr, dstTy, vReg, vTy, isVol)
    Return
  End If

  If k = NdIndexAssign Then
    Dim tmpIa As Integer = NewNode(NdIndexAccess)
    g_nodes(tmpIa).left = g_nodes(id).left
    g_nodes(tmpIa).right = g_nodes(id).right
    Dim vTy As TypeInfo
    Dim vReg As Integer = CgEmitExpr(g_nodes(id).third, ByRef vTy)
    Dim dstTy As TypeInfo
    Dim isInd = 0, isVol = 0 As Integer
    Dim dstPtr As Integer = CgEmitLValuePtr(tmpIa, ByRef dstTy, ByRef isInd, ByRef isVol)
    CgEmitStoreTypedEx(dstPtr, dstTy, vReg, vTy, isVol)
    Return
  End If

  If k = NdExprStmt Then
    Dim dummyTy As TypeInfo
    CgEmitExpr(g_nodes(id).left, ByRef dummyTy)
    Return
  End If

  If k = NdReturn Then
    If g_nodes(id).left < 0 Then
      fprintf(cg_out, "  ret void\n")
      cg_blockEnded = 1
      Return
    End If
    Dim vTy As TypeInfo
    Dim vReg As Integer = CgEmitExpr(g_nodes(id).left, ByRef vTy)
    If CgIsAggregate(cg_curRetTy) Then
      Dim loaded As Integer = CgNextReg()
      fprintf(cg_out, "  %%r%d = load ", loaded)
      CgEmitLlvmType(cg_curRetTy)
      fprintf(cg_out, ", ptr %%r%d, align 8\n", vReg)
      fprintf(cg_out, "  ret ")
      CgEmitLlvmType(cg_curRetTy)
      fprintf(cg_out, " %%r%d\n", loaded)
    Else
      Dim coerced As Integer = CgCoerceScalar(vReg, vTy, cg_curRetTy)
      fprintf(cg_out, "  ret ")
      CgEmitLlvmType(cg_curRetTy)
      fprintf(cg_out, " %%r%d\n", coerced)
    End If
    cg_blockEnded = 1
    Return
  End If

  If k = NdAssert Then
    Dim cTy As TypeInfo
    Dim cReg As Integer = CgEmitExpr(g_nodes(id).left, ByRef cTy)
    Dim cBit As Integer = cReg
    If cTy.kind <> TyBoolean Then
      cBit = CgNextReg()
      fprintf(cg_out, "  %%r%d = icmp ne ", cBit)
      CgEmitLlvmType(cTy)
      fprintf(cg_out, " %%r%d, 0\n", cReg)
    End If
    Dim lOk As Integer = CgNextLbl()
    Dim lFail As Integer = CgNextLbl()
    fprintf(cg_out, "  br i1 %%r%d, label %%L%d, label %%L%d\n", cBit, lOk, lFail)
    fprintf(cg_out, "L%d:\n", lFail)
    Dim msgNode As Integer = g_nodes(id).right
    If msgNode >= 0 Then
      Dim mTy As TypeInfo
      Dim mReg As Integer = CgEmitExpr(msgNode, ByRef mTy)
      fprintf(cg_out, "  call void @__vb_assert_fail(i32 %d, ptr %%r%d)\n", g_nodes(id).intVal, mReg)
    Else
      fprintf(cg_out, "  call void @__vb_assert_fail(i32 %d, ptr null)\n", g_nodes(id).intVal)
    End If
    If cg_inTestFn = 1 Then
      fprintf(cg_out, "  ret void\n")
    Else
      fprintf(cg_out, "  br label %%L%d\n", lOk)
    End If
    fprintf(cg_out, "L%d:\n", lOk)
    Return
  End If

  If k = NdIf Then
    Dim cTy As TypeInfo
    Dim cReg As Integer = CgEmitExpr(g_nodes(id).left, ByRef cTy)
    Dim cBit As Integer = cReg
    If cTy.kind <> TyBoolean Then
      cBit = CgNextReg()
      fprintf(cg_out, "  %%r%d = icmp ne ", cBit)
      CgEmitLlvmType(cTy)
      fprintf(cg_out, " %%r%d, 0\n", cReg)
    End If
    Dim lThen As Integer = CgNextLbl()
    Dim lElse As Integer = CgNextLbl()
    Dim lCont As Integer = CgNextLbl()

    fprintf(cg_out, "  br i1 %%r%d, label %%L%d, label %%L%d\n", cBit, lThen, lElse)
    fprintf(cg_out, "L%d:\n", lThen)
    cg_blockEnded = 0
    Dim saved1 As Integer = cg_localCount
    Dim i As Integer = 0
    While i < g_nodes(id).childCount Do
      CgEmitStmt(g_kids(g_nodes(id).firstChild + i))
      i = i + 1
    End While
    cg_localCount = saved1
    Dim thenEnded As Boolean = (cg_blockEnded = 1)
    If Not thenEnded Then
      fprintf(cg_out, "  br label %%L%d\n", lCont)
    End If

    fprintf(cg_out, "L%d:\n", lElse)
    cg_blockEnded = 0
    Dim saved2 As Integer = cg_localCount
    For j = 0 To g_nodes(id).extraCount - 1 Do
      CgEmitStmt(g_kids(g_nodes(id).extraStart + j))
    End For
    cg_localCount = saved2
    Dim elseEnded As Boolean = (cg_blockEnded = 1)
    If Not elseEnded Then
      fprintf(cg_out, "  br label %%L%d\n", lCont)
    End If

    If thenEnded And elseEnded Then
      cg_blockEnded = 1
    Else
      fprintf(cg_out, "L%d:\n", lCont)
      cg_blockEnded = 0
    End If
    Return
  End If

  If k = NdWhile Then
    Dim lCond As Integer = CgNextLbl()
    Dim lBody As Integer = CgNextLbl()
    Dim lEnd As Integer = CgNextLbl()
    fprintf(cg_out, "  br label %%L%d\n", lCond)
    fprintf(cg_out, "L%d:\n", lCond)
    cg_blockEnded = 0
    Dim cTy As TypeInfo
    Dim cReg As Integer = CgEmitExpr(g_nodes(id).left, ByRef cTy)
    Dim cBit As Integer = cReg
    If cTy.kind <> TyBoolean Then
      cBit = CgNextReg()
      fprintf(cg_out, "  %%r%d = icmp ne ", cBit)
      CgEmitLlvmType(cTy)
      fprintf(cg_out, " %%r%d, 0\n", cReg)
    End If
    fprintf(cg_out, "  br i1 %%r%d, label %%L%d, label %%L%d\n", cBit, lBody, lEnd)
    fprintf(cg_out, "L%d:\n", lBody)
    cg_blockEnded = 0
    Dim saved As Integer = cg_localCount
    cg_loopBreakLbls(cg_loopDepth) = lEnd
    cg_loopContLbls(cg_loopDepth) = lCond
    cg_loopDepth = cg_loopDepth + 1
    Dim i As Integer = 0
    While i < g_nodes(id).childCount Do
      CgEmitStmt(g_kids(g_nodes(id).firstChild + i))
      i = i + 1
    End While
    cg_loopDepth = cg_loopDepth - 1
    cg_localCount = saved
    If cg_blockEnded = 0 Then
      fprintf(cg_out, "  br label %%L%d\n", lCond)
    End If
    fprintf(cg_out, "L%d:\n", lEnd)
    cg_blockEnded = 0
    Return
  End If

  If k = NdFor Then
    Dim lCond As Integer = CgNextLbl()
    Dim lBody As Integer = CgNextLbl()
    Dim lInc As Integer = CgNextLbl()
    Dim lEnd As Integer = CgNextLbl()
    Dim sTy As TypeInfo
    Dim sReg As Integer = CgEmitExpr(g_nodes(id).left, ByRef sTy)
    Dim intTy As TypeInfo With ( kind = TyInt, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
    Dim s32 As Integer = CgCoerceScalar(sReg, sTy, intTy)

    Dim saved As Integer = cg_localCount
    Dim slot As Integer = -1
    Dim existing As Integer = CgFindLocal(g_nodes(id).intVal, g_nodes(id).lenVal)
    If existing >= 0 Then
      slot = cg_locals(existing).slotReg
    Else
      slot = CgNextReg()
      fprintf(cg_out, "  %%r%d = alloca i32, align 4\n", slot)
      cg_locals(cg_localCount) = CgLocalSym With (
        namePos = g_nodes(id).intVal,
        nameLen = g_nodes(id).lenVal,
        slotReg = slot,
        isIndirect = 0,
        ty = intTy
      )
      cg_localCount = cg_localCount + 1
    End If
    fprintf(cg_out, "  store i32 %%r%d, ptr %%r%d, align 4\n", s32, slot)
    fprintf(cg_out, "  br label %%L%d\n", lCond)

    fprintf(cg_out, "L%d:\n", lCond)
    cg_blockEnded = 0
    Dim curV As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = load i32, ptr %%r%d, align 4\n", curV, slot)
    Dim fTy As TypeInfo
    Dim fReg As Integer = CgEmitExpr(g_nodes(id).right, ByRef fTy)
    Dim f32 As Integer = CgCoerceScalar(fReg, fTy, intTy)
    Dim cmp As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = icmp sle i32 %%r%d, %%r%d\n", cmp, curV, f32)
    fprintf(cg_out, "  br i1 %%r%d, label %%L%d, label %%L%d\n", cmp, lBody, lEnd)

    fprintf(cg_out, "L%d:\n", lBody)
    cg_blockEnded = 0
    Dim savedBody As Integer = cg_localCount
    cg_loopBreakLbls(cg_loopDepth) = lEnd
    cg_loopContLbls(cg_loopDepth) = lInc
    cg_loopDepth = cg_loopDepth + 1
    For i = 0 To g_nodes(id).childCount - 1 Do
      CgEmitStmt(g_kids(g_nodes(id).firstChild + i))
    End For
    cg_loopDepth = cg_loopDepth - 1
    cg_localCount = savedBody
    If cg_blockEnded = 0 Then
      fprintf(cg_out, "  br label %%L%d\n", lInc)
    End If

    fprintf(cg_out, "L%d:\n", lInc)
    cg_blockEnded = 0
    Dim relV As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = load i32, ptr %%r%d, align 4\n", relV, slot)
    Dim incV As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = add nsw i32 %%r%d, 1\n", incV, relV)
    fprintf(cg_out, "  store i32 %%r%d, ptr %%r%d, align 4\n", incV, slot)
    fprintf(cg_out, "  br label %%L%d\n", lCond)

    fprintf(cg_out, "L%d:\n", lEnd)
    cg_blockEnded = 0
    cg_localCount = saved
    Return
  End If

  If k = NdWith Then
    Dim tTy As TypeInfo
    Dim tInd = 0, tVol = 0 As Integer
    Dim tPtr As Integer = CgEmitLValuePtr(g_nodes(id).left, ByRef tTy, ByRef tInd, ByRef tVol)
    cg_withPtrs(cg_withDepth) = tPtr
    cg_withTys(cg_withDepth) = tTy
    cg_withVols(cg_withDepth) = tVol
    cg_withDepth = cg_withDepth + 1
    Dim savedW As Integer = cg_localCount
    For wi = 0 To g_nodes(id).childCount - 1 Do
      CgEmitStmt(g_kids(g_nodes(id).firstChild + wi))
    End For
    cg_localCount = savedW
    cg_withDepth = cg_withDepth - 1
    Return
  End If

  If k = NdMatch Then
    Dim isCondMatch As Boolean = (g_nodes(id).op = 1)
    Dim tTy As TypeInfo
    Dim tReg As Integer = 0
    If Not isCondMatch Then
      tReg = CgEmitExpr(g_nodes(id).left, ByRef tTy)
    End If
    Dim tIsStr As Boolean = (Not isCondMatch) And (((tTy.arrLen = 0) And (tTy.kind = TyPointer)) Or ((tTy.arrLen <> 0) And (tTy.kind = TyByte)))
    Dim lExit As Integer = CgNextLbl()
    Dim anyFalls As Boolean = False
    Dim hasUncondWild As Boolean = False

    Dim brStart As Integer = g_nodes(id).firstChild
    Dim brCount As Integer = g_nodes(id).childCount
    Dim bi As Integer = 0
    While (bi < brCount) And (Not hasUncondWild) Do
      Dim arm As Integer = g_kids(brStart + bi)
      Dim isWild As Boolean = (g_nodes(arm).op = 1)
      Dim guardNode As Integer = g_nodes(arm).left
      Dim lBody As Integer = CgNextLbl()
      Dim lNext As Integer = CgNextLbl()

      If isWild And (guardNode < 0) Then
        hasUncondWild = True
        fprintf(cg_out, "  br label %%L%d\n", lBody)
      Else
        If isWild Then
          Dim gTy As TypeInfo
          Dim gReg As Integer = CgEmitExpr(guardNode, ByRef gTy)
          Dim gBit As Integer = gReg
          If gTy.kind <> TyBoolean Then
            gBit = CgNextReg()
            fprintf(cg_out, "  %%r%d = icmp ne ", gBit)
            CgEmitLlvmType(gTy)
            fprintf(cg_out, " %%r%d, 0\n", gReg)
          End If
          fprintf(cg_out, "  br i1 %%r%d, label %%L%d, label %%L%d\n", gBit, lBody, lNext)
        Else
          Dim lGuard As Integer = lBody
          If guardNode >= 0 Then
            lGuard = CgNextLbl()
          End If
          Dim vStart As Integer = g_nodes(arm).extraStart
          Dim vCount As Integer = g_nodes(arm).extraCount
          For vi = 0 To vCount - 1 Do
            Dim lTryNext As Integer = lNext
            If (vi + 1) < vCount Then
              lTryNext = CgNextLbl()
            End If
            Dim pe As Integer = g_kids(vStart + vi)
            Dim cTy As TypeInfo
            Dim cReg As Integer = CgEmitExpr(pe, ByRef cTy)
            Dim eqR As Integer = 0
            If isCondMatch Then
              eqR = cReg
              If cTy.kind <> TyBoolean Then
                eqR = CgNextReg()
                fprintf(cg_out, "  %%r%d = icmp ne ", eqR)
                CgEmitLlvmType(cTy)
                fprintf(cg_out, " %%r%d, 0\n", cReg)
              End If
            Else
              If tIsStr Then
                If (g_nodes(pe).kind = NdIntLit) And (g_nodes(pe).intVal = 0) Then
                  eqR = CgNextReg()
                  fprintf(cg_out, "  %%r%d = icmp eq ptr %%r%d, null\n", eqR, tReg)
                Else
                  cg_usesStrEq = 1
                  eqR = CgNextReg()
                  fprintf(cg_out, "  %%r%d = call i1 @__vb_str_eq(ptr %%r%d, ptr %%r%d)\n", eqR, tReg, cReg)
                End If
              Else
                Dim coerced As Integer = CgCoerceScalar(cReg, cTy, tTy)
                eqR = CgNextReg()
                If CgIsFloat(tTy.kind) Then
                  fprintf(cg_out, "  %%r%d = fcmp oeq ", eqR)
                Else
                  fprintf(cg_out, "  %%r%d = icmp eq ", eqR)
                End If
                CgEmitLlvmType(tTy)
                fprintf(cg_out, " %%r%d, %%r%d\n", tReg, coerced)
              End If
            End If
            fprintf(cg_out, "  br i1 %%r%d, label %%L%d, label %%L%d\n", eqR, lGuard, lTryNext)
            If (vi + 1) < vCount Then
              fprintf(cg_out, "L%d:\n", lTryNext)
            End If
          End For
          If guardNode >= 0 Then
            fprintf(cg_out, "L%d:\n", lGuard)
            Dim gTy As TypeInfo
            Dim gReg As Integer = CgEmitExpr(guardNode, ByRef gTy)
            Dim gBit As Integer = gReg
            If gTy.kind <> TyBoolean Then
              gBit = CgNextReg()
              fprintf(cg_out, "  %%r%d = icmp ne ", gBit)
              CgEmitLlvmType(gTy)
              fprintf(cg_out, " %%r%d, 0\n", gReg)
            End If
            fprintf(cg_out, "  br i1 %%r%d, label %%L%d, label %%L%d\n", gBit, lBody, lNext)
          End If
        End If
      End If

      fprintf(cg_out, "L%d:\n", lBody)
      cg_blockEnded = 0
      Dim savedB As Integer = cg_localCount
      For si = 0 To g_nodes(arm).childCount - 1 Do
        CgEmitStmt(g_kids(g_nodes(arm).firstChild + si))
      End For
      cg_localCount = savedB
      If cg_blockEnded = 0 Then
        anyFalls = True
        fprintf(cg_out, "  br label %%L%d\n", lExit)
      End If
      If Not hasUncondWild Then
        fprintf(cg_out, "L%d:\n", lNext)
        cg_blockEnded = 0
      End If
      bi = bi + 1
    End While

    If Not hasUncondWild Then
      anyFalls = True
      fprintf(cg_out, "  br label %%L%d\n", lExit)
    End If
    If anyFalls Then
      fprintf(cg_out, "L%d:\n", lExit)
      cg_blockEnded = 0
    Else
      cg_blockEnded = 1
    End If
    Return
  End If
End Sub

Private Sub CgEmitAsyncFunc(fnNodeId As Integer, fnIdx As Integer)
  Dim fi As CgFuncInfo = cg_funcs(fnIdx)
  Dim hasRes As Integer = 0
  If fi.bodyRetTy.kind <> TyNothing Then
    hasRes = 1
  End If
  Dim pBase As Integer = 2 + hasRes

  ' 1) Emit internal body function @__vb_async_body_<sym>
  cg_reg = 1
  cg_localCount = 0
  cg_blockEnded = 0
  cg_loopDepth = 0
  cg_curRetTy = fi.bodyRetTy
  cg_curModId = fi.modId

  fprintf(cg_out, "define internal ")
  CgEmitLlvmType(fi.bodyRetTy)
  fprintf(cg_out, " @__vb_async_body_")
  CgEmitSymName(fi.namePos, fi.nameLen, fi.modId, fi.isPrivate)
  fprintf(cg_out, "(")
  For i = 0 To fi.paramCount - 1 Do
    If i > 0 Then
      fprintf(cg_out, ", ")
    End If
    CgEmitParamLlvmType(cg_params(fi.paramStart + i))
    fprintf(cg_out, " %%p%d", i)
  End For
  fprintf(cg_out, ") {\nentry:\n")

  For j = 0 To fi.paramCount - 1 Do
    Dim p As CgParamInfo = cg_params(fi.paramStart + j)
    Dim directPtr As Boolean = (p.isByRef = 1) Or (p.ty.arrLen < 0)
    If directPtr Then
      cg_locals(cg_localCount) = CgLocalSym With (
        namePos = p.namePos,
        nameLen = p.nameLen,
        slotReg = -(j + 1),
        isIndirect = 0,
        ty = p.ty
      )
      cg_localCount = cg_localCount + 1
    Else
      Dim slot As Integer = CgNextReg()
      fprintf(cg_out, "  %%r%d = alloca ", slot)
      CgEmitLlvmType(p.ty)
      fprintf(cg_out, ", align 8\n")
      If CgIsAggregate(p.ty) Then
        Dim sz As Integer = CgTypeSize(p.ty)
        fprintf(cg_out, "  call void @llvm.memcpy.p0.p0.i64(ptr align 8 %%r%d, ptr align 8 %%p%d, i64 %d, i1 false)\n", slot, j, sz)
      Else
        Dim align As Integer = CgTypeAlign(p.ty)
        fprintf(cg_out, "  store ")
        CgEmitLlvmType(p.ty)
        fprintf(cg_out, " %%p%d, ptr %%r%d, align %d\n", j, slot, align)
      End If
      cg_locals(cg_localCount) = CgLocalSym With (
        namePos = p.namePos,
        nameLen = p.nameLen,
        slotReg = slot,
        isIndirect = 0,
        ty = p.ty
      )
      cg_localCount = cg_localCount + 1
    End If
  End For

  Dim bStart = g_nodes(fnNodeId).firstChild, bCount = g_nodes(fnNodeId).childCount, k = 0 As Integer
  While k < bCount Do
    CgEmitStmt(g_kids(bStart + k))
    k = k + 1
  End While
  If cg_blockEnded = 0 Then
    If fi.bodyRetTy.kind = TyNothing Then
      fprintf(cg_out, "  ret void\n")
    Else
      fprintf(cg_out, "  ret ")
      CgEmitLlvmType(fi.bodyRetTy)
      fprintf(cg_out, " ")
      CgEmitZeroVal(fi.bodyRetTy)
      fprintf(cg_out, "\n")
    End If
  End If
  fprintf(cg_out, "}\n\n")

  ' 2) Emit pthread/deferred thunk @__vb_async_thunk_<sym>(ptr %frame) -> ptr
  cg_reg = 1
  fprintf(cg_out, "define internal ptr @__vb_async_thunk_")
  CgEmitSymName(fi.namePos, fi.nameLen, fi.modId, fi.isPrivate)
  fprintf(cg_out, "(ptr %%frame) {\nentry:\n")
  Dim argRegs(32) As Integer
  For j = 0 To fi.paramCount - 1 Do
    Dim p As CgParamInfo = cg_params(fi.paramStart + j)
    Dim fpReg As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = getelementptr inbounds %%struct.__vb_async_", fpReg)
    CgEmitSymName(fi.namePos, fi.nameLen, fi.modId, fi.isPrivate)
    fprintf(cg_out, ", ptr %%frame, i32 0, i32 %d\n", pBase + j)
    If (p.isByRef = 1) Or (p.ty.arrLen < 0) Then
      Dim lv As Integer = CgNextReg()
      fprintf(cg_out, "  %%r%d = load ptr, ptr %%r%d, align %d\n", lv, fpReg, cg_ptrAlign)
      argRegs(j) = lv
    Else
      If CgIsAggregate(p.ty) Then
        argRegs(j) = fpReg
      Else
        Dim lv As Integer = CgNextReg()
        fprintf(cg_out, "  %%r%d = load ", lv)
        CgEmitLlvmType(p.ty)
        fprintf(cg_out, ", ptr %%r%d, align %d\n", fpReg, CgTypeAlign(p.ty))
        argRegs(j) = lv
      End If
    End If
  End For
  Dim resReg As Integer = 0
  If hasRes = 1 Then
    resReg = CgNextReg()
    fprintf(cg_out, "  %%r%d = call ", resReg)
    CgEmitLlvmType(fi.bodyRetTy)
  Else
    fprintf(cg_out, "  call void")
  End If
  fprintf(cg_out, " @__vb_async_body_")
  CgEmitSymName(fi.namePos, fi.nameLen, fi.modId, fi.isPrivate)
  fprintf(cg_out, "(")
  For j = 0 To fi.paramCount - 1 Do
    If j > 0 Then
      fprintf(cg_out, ", ")
    End If
    CgEmitParamLlvmType(cg_params(fi.paramStart + j))
    fprintf(cg_out, " %%r%d", argRegs(j))
  End For
  fprintf(cg_out, ")\n")
  If hasRes = 1 Then
    Dim rpReg As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = getelementptr inbounds %%struct.__vb_async_", rpReg)
    CgEmitSymName(fi.namePos, fi.nameLen, fi.modId, fi.isPrivate)
    fprintf(cg_out, ", ptr %%frame, i32 0, i32 2\n")
    fprintf(cg_out, "  store ")
    CgEmitLlvmType(fi.bodyRetTy)
    fprintf(cg_out, " %%r%d, ptr %%r%d, align %d\n", resReg, rpReg, CgTypeAlign(fi.bodyRetTy))
  End If
  fprintf(cg_out, "  ret ptr null\n}\n\n")

  ' 3) Emit launcher @<sym>(params...) -> ptr (Task(T))
  cg_reg = 1
  If fi.isPublic = 1 Then
    fprintf(cg_out, "define ptr @")
  Else
    fprintf(cg_out, "define internal ptr @")
  End If
  CgEmitSymName(fi.namePos, fi.nameLen, fi.modId, fi.isPrivate)
  fprintf(cg_out, "(")
  For i = 0 To fi.paramCount - 1 Do
    If i > 0 Then
      fprintf(cg_out, ", ")
    End If
    CgEmitParamLlvmType(cg_params(fi.paramStart + i))
    fprintf(cg_out, " %%p%d", i)
  End For
  fprintf(cg_out, ") {\nentry:\n")
  Dim szp = CgNextReg(), sz = CgNextReg(), frm = CgNextReg(), thkSlot = CgNextReg() As Integer
  fprintf(cg_out, "  %%r%d = getelementptr %%struct.__vb_async_", szp)
  CgEmitSymName(fi.namePos, fi.nameLen, fi.modId, fi.isPrivate)
  fprintf(cg_out, ", ptr null, i32 1\n")
  fprintf(cg_out, "  %%r%d = ptrtoint ptr %%r%d to i64\n", sz, szp)
  fprintf(cg_out, "  %%r%d = call ptr @calloc(i64 1, i64 %%r%d)\n", frm, sz)
  fprintf(cg_out, "  %%r%d = getelementptr inbounds %%struct.__vb_async_", thkSlot)
  CgEmitSymName(fi.namePos, fi.nameLen, fi.modId, fi.isPrivate)
  fprintf(cg_out, ", ptr %%r%d, i32 0, i32 1\n", frm)
  fprintf(cg_out, "  store ptr @__vb_async_thunk_")
  CgEmitSymName(fi.namePos, fi.nameLen, fi.modId, fi.isPrivate)
  fprintf(cg_out, ", ptr %%r%d, align %d\n", thkSlot, cg_ptrAlign)

  For j = 0 To fi.paramCount - 1 Do
    Dim p As CgParamInfo = cg_params(fi.paramStart + j)
    Dim dstReg As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = getelementptr inbounds %%struct.__vb_async_", dstReg)
    CgEmitSymName(fi.namePos, fi.nameLen, fi.modId, fi.isPrivate)
    fprintf(cg_out, ", ptr %%r%d, i32 0, i32 %d\n", frm, pBase + j)
    If (p.isByRef = 1) Or (p.ty.arrLen < 0) Then
      fprintf(cg_out, "  store ptr %%p%d, ptr %%r%d, align %d\n", j, dstReg, cg_ptrAlign)
    Else
      If CgIsAggregate(p.ty) Then
        Dim al = CgTypeAlign(p.ty), psz = CgTypeSize(p.ty) As Integer
        fprintf(cg_out, "  call void @llvm.memcpy.p0.p0.i64(ptr align %d %%r%d, ptr align %d %%p%d, i64 %d, i1 false)\n", al, dstReg, al, j, psz)
      Else
        fprintf(cg_out, "  store ")
        CgEmitLlvmType(p.ty)
        fprintf(cg_out, " %%p%d, ptr %%r%d, align %d\n", j, dstReg, CgTypeAlign(p.ty))
      End If
    End If
  End For

  If cg_isFreestanding = 0 Then
    Dim tidSlot As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = getelementptr inbounds %%struct.__vb_async_", tidSlot)
    CgEmitSymName(fi.namePos, fi.nameLen, fi.modId, fi.isPrivate)
    fprintf(cg_out, ", ptr %%r%d, i32 0, i32 0\n", frm)
    fprintf(cg_out, "  call i32 @pthread_create(ptr %%r%d, ptr null, ptr @__vb_async_thunk_", tidSlot)
    CgEmitSymName(fi.namePos, fi.nameLen, fi.modId, fi.isPrivate)
    fprintf(cg_out, ", ptr %%r%d)\n", frm)
  End If
  fprintf(cg_out, "  ret ptr %%r%d\n}\n\n", frm)
End Sub

Private Sub CgEmitFunc(fnNodeId As Integer, fnIdx As Integer)
  Dim fi As CgFuncInfo = cg_funcs(fnIdx)
  If fi.isAsync = 1 Then
    CgEmitAsyncFunc(fnNodeId, fnIdx)
    Return
  End If
  cg_reg = 1
  cg_localCount = 0
  cg_blockEnded = 0
  cg_loopDepth = 0
  cg_curRetTy = fi.retTy
  cg_curModId = fi.modId

  Dim renameMain As Boolean = (cg_testMode = 1) And (cg_testCount > 0) And SliceEqualsStr(fi.namePos, fi.nameLen, "main", 4)
  If (fi.isPublic = 1) And (Not renameMain) Then
    fprintf(cg_out, "define ")
  Else
    fprintf(cg_out, "define internal ")
  End If
  CgEmitLlvmType(fi.retTy)
  fprintf(cg_out, " @")
  If renameMain Then
    fprintf(cg_out, "__vb_user_main")
  Else
    CgEmitSymName(fi.namePos, fi.nameLen, fi.modId, fi.isPrivate)
  End If
  fprintf(cg_out, "(")
  For i = 0 To fi.paramCount - 1 Do
    If i > 0 Then
      fprintf(cg_out, ", ")
    End If
    CgEmitParamLlvmType(cg_params(fi.paramStart + i))
    fprintf(cg_out, " %%p%d", i)
  End For
  fprintf(cg_out, ") {\nentry:\n")

  ' Bind parameters to local slots
  For j = 0 To fi.paramCount - 1 Do
    Dim p As CgParamInfo = cg_params(fi.paramStart + j)
    Dim directPtr As Boolean = (p.isByRef = 1) Or (p.ty.arrLen < 0)
    If directPtr Then
      cg_locals(cg_localCount) = CgLocalSym With (
        namePos = p.namePos,
        nameLen = p.nameLen,
        slotReg = -(j + 1),
        isIndirect = 0,
        ty = p.ty
      )
      cg_localCount = cg_localCount + 1
    Else
      Dim slot As Integer = CgNextReg()
      fprintf(cg_out, "  %%r%d = alloca ", slot)
      CgEmitLlvmType(p.ty)
      fprintf(cg_out, ", align 8\n")
      If CgIsAggregate(p.ty) Then
        Dim sz As Integer = CgTypeSize(p.ty)
        fprintf(cg_out, "  call void @llvm.memcpy.p0.p0.i64(ptr align 8 %%r%d, ptr align 8 %%p%d, i64 %d, i1 false)\n", slot, j, sz)
      Else
        Dim align As Integer = CgTypeAlign(p.ty)
        fprintf(cg_out, "  store ")
        CgEmitLlvmType(p.ty)
        fprintf(cg_out, " %%p%d, ptr %%r%d, align %d\n", j, slot, align)
      End If
      cg_locals(cg_localCount) = CgLocalSym With (
        namePos = p.namePos,
        nameLen = p.nameLen,
        slotReg = slot,
        isIndirect = 0,
        ty = p.ty
      )
      cg_localCount = cg_localCount + 1
    End If
  End For

  Dim bStart As Integer = g_nodes(fnNodeId).firstChild
  Dim bCount As Integer = g_nodes(fnNodeId).childCount
  For k = 0 To bCount - 1 Do
    CgEmitStmt(g_kids(bStart + k))
  End For

  If cg_blockEnded = 0 Then
    If fi.retTy.kind = TyNothing Then
      fprintf(cg_out, "  ret void\n")
    Else
      fprintf(cg_out, "  ret ")
      CgEmitLlvmType(fi.retTy)
      fprintf(cg_out, " ")
      CgEmitZeroVal(fi.retTy)
      fprintf(cg_out, "\n")
    End If
  End If
  fprintf(cg_out, "}\n\n")
End Sub

Private Sub CgEmitOpDef(opNodeId As Integer, opIdx As Integer)
  Dim oi As CgOpInfo = cg_ops(opIdx)
  cg_reg = 1
  cg_localCount = 0
  cg_blockEnded = 0
  cg_loopDepth = 0
  cg_curRetTy = oi.retTy
  cg_curModId = oi.modId

  If oi.isPublic = 1 Then
    fprintf(cg_out, "define ")
  Else
    fprintf(cg_out, "define internal ")
  End If
  CgEmitLlvmType(oi.retTy)
  fprintf(cg_out, " @")
  CgEmitOpSymName(opIdx)
  fprintf(cg_out, "(")
  For i = 0 To oi.paramCount - 1 Do
    If i > 0 Then
      fprintf(cg_out, ", ")
    End If
    CgEmitParamLlvmType(cg_params(oi.paramStart + i))
    fprintf(cg_out, " %%p%d", i)
  End For
  fprintf(cg_out, ") {\nentry:\n")

  For j = 0 To oi.paramCount - 1 Do
    Dim p As CgParamInfo = cg_params(oi.paramStart + j)
    Dim directPtr As Boolean = (p.isByRef = 1) Or (p.ty.arrLen < 0)
    If directPtr Then
      cg_locals(cg_localCount) = CgLocalSym With (
        namePos = p.namePos,
        nameLen = p.nameLen,
        slotReg = -(j + 1),
        isIndirect = 0,
        ty = p.ty
      )
      cg_localCount = cg_localCount + 1
    Else
      Dim slot As Integer = CgNextReg()
      fprintf(cg_out, "  %%r%d = alloca ", slot)
      CgEmitLlvmType(p.ty)
      fprintf(cg_out, ", align 8\n")
      If CgIsAggregate(p.ty) Then
        Dim sz As Integer = CgTypeSize(p.ty)
        fprintf(cg_out, "  call void @llvm.memcpy.p0.p0.i64(ptr align 8 %%r%d, ptr align 8 %%p%d, i64 %d, i1 false)\n", slot, j, sz)
      Else
        Dim align As Integer = CgTypeAlign(p.ty)
        fprintf(cg_out, "  store ")
        CgEmitLlvmType(p.ty)
        fprintf(cg_out, " %%p%d, ptr %%r%d, align %d\n", j, slot, align)
      End If
      cg_locals(cg_localCount) = CgLocalSym With (
        namePos = p.namePos,
        nameLen = p.nameLen,
        slotReg = slot,
        isIndirect = 0,
        ty = p.ty
      )
      cg_localCount = cg_localCount + 1
    End If
  End For

  Dim bStart As Integer = g_nodes(opNodeId).firstChild
  Dim bCount As Integer = g_nodes(opNodeId).childCount
  For k = 0 To bCount - 1 Do
    CgEmitStmt(g_kids(bStart + k))
  End For

  If cg_blockEnded = 0 Then
    fprintf(cg_out, "  ret ")
    CgEmitLlvmType(oi.retTy)
    fprintf(cg_out, " ")
    CgEmitZeroVal(oi.retTy)
    fprintf(cg_out, "\n")
  End If
  fprintf(cg_out, "}\n\n")
End Sub

Private Sub CgEmitTestFn(tdNodeId As Integer, testIdx As Integer)
  cg_reg = 1
  cg_localCount = 0
  cg_blockEnded = 0
  cg_loopDepth = 0
  cg_curModId = g_nodes(tdNodeId).modId
  cg_curRetTy = TypeInfo With ( kind = TyNothing, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
  cg_inTestFn = 1

  fprintf(cg_out, "define internal void @__vb_test_%d() {\nentry:\n", testIdx)
  Dim bStart As Integer = g_nodes(tdNodeId).firstChild
  Dim bCount As Integer = g_nodes(tdNodeId).childCount
  For k = 0 To bCount - 1 Do
    CgEmitStmt(g_kids(bStart + k))
  End For
  If cg_blockEnded = 0 Then
    fprintf(cg_out, "  ret void\n")
  End If
  fprintf(cg_out, "}\n\n")
  cg_inTestFn = 0
End Sub

Private Sub CgEmitAssertRuntime()
  If (cg_testMode = 1) And (cg_testCount > 0) Then
    If Not CgHasExtern("fflush", 6) Then
      fputs("declare i32 @fflush(ptr)\n", cg_out)
    End If
    fputs(IR_ASSERT_TEST_DECLS, cg_out)
    fputs(IR_ASSERT_TEST_FN, cg_out)
    Return
  End If

  If cg_usesAssert = 1 Then
    If Not CgHasExtern("exit", 4) Then
      fputs("declare void @exit(i32)\n", cg_out)
    End If
    If Not CgHasExtern("fflush", 6) Then
      fputs("declare i32 @fflush(ptr)\n", cg_out)
    End If
    fputs(IR_ASSERT_NORM_DECLS, cg_out)
    fputs(IR_ASSERT_NORM_FN, cg_out)
  End If
End Sub

Private Sub CgEmitTestMain(progRoot As Integer)
  Dim defStart As Integer = g_nodes(progRoot).firstChild
  Dim defCount As Integer = g_nodes(progRoot).childCount
  fputs(IR_TEST_MAIN_PROLOGUE, cg_out)

  Dim tIdx As Integer = 0
  For i = 0 To defCount - 1 Do
    Dim d As Integer = g_kids(defStart + i)
    If g_nodes(d).kind = NdTestDef Then
      Dim nameStrId As Integer = g_nodes(d).op
      fprintf(cg_out, "  store i32 0, ptr @__vb_test_cur_fail, align 4\n  call void @__vb_test_%d()\n  %%tf_%d = load i32, ptr @__vb_test_cur_fail, align 4\n  %%tok_%d = icmp eq i32 %%tf_%d, 0\n  br i1 %%tok_%d, label %%tpass_%d, label %%tfail_%d\ntpass_%d:\n", tIdx, tIdx, tIdx, tIdx, tIdx, tIdx, tIdx, tIdx)
      fprintf(cg_out, "  call i32 (ptr, ...) @printf(ptr @__vb_test_pass_fmt, ptr @.str_%d)\n  call i32 @fflush(ptr null)\n  %%p0_%d = load i32, ptr %%passed, align 4\n  %%p1_%d = add i32 %%p0_%d, 1\n  store i32 %%p1_%d, ptr %%passed, align 4\n  br label %%tnext_%d\ntfail_%d:\n", nameStrId, tIdx, tIdx, tIdx, tIdx, tIdx, tIdx)
      fprintf(cg_out, "  call i32 (ptr, ...) @printf(ptr @__vb_test_fail_fmt, ptr @.str_%d)\n  call i32 @fflush(ptr null)\n  %%f0_%d = load i32, ptr %%failed, align 4\n  %%f1_%d = add i32 %%f0_%d, 1\n  store i32 %%f1_%d, ptr %%failed, align 4\n  br label %%tnext_%d\ntnext_%d:\n", nameStrId, tIdx, tIdx, tIdx, tIdx, tIdx, tIdx)
      tIdx = tIdx + 1
    End If
  End For

  fputs(IR_TEST_MAIN_EPILOGUE, cg_out)
End Sub

Private Sub CgEmitArenaRuntime()
  fputs(IR_ARENA_RT_INIT_HEAP, cg_out)
  fputs(IR_ARENA_RT_INIT_BUF, cg_out)
  fputs(IR_ARENA_RT_ALLOC, cg_out)
  fputs(IR_ARENA_RT_OPS, cg_out)
  fputs(IR_ARENA_RT_REWIND_DESTROY, cg_out)
End Sub

Private Sub CgEmitStrConcatRuntime()
  If Not CgHasExtern("strlen", 6) Then
    fputs("declare i64 @strlen(ptr)\n", cg_out)
  End If
  If (cg_isFreestanding = 1) And (cg_usesCalloc = 0) And (Not CgHasExtern("calloc", 6)) Then
    fputs("declare ptr @calloc(i64, i64)\n", cg_out)
  End If
  fputs(IR_STR_CONCAT_RT, cg_out)
End Sub

Private Sub CgEmitStrEqRuntime()
  If Not CgHasExtern("strcmp", 6) Then
    fputs("declare i32 @strcmp(ptr, ptr)\n", cg_out)
  End If
  fputs(IR_STR_EQ_RT, cg_out)
End Sub

Private Function CgHasExtern(name As Pointer, nLen As Integer) As Boolean
  For i = 0 To cg_funcCount - 1 Do
    If (cg_funcs(i).isExtern = 1) And SliceEqualsStr(cg_funcs(i).namePos, cg_funcs(i).nameLen, name, nLen) Then
      Return True
    End If
  End For
  Return False
End Function

Public Function EmitProgramToFile(ByRef arena As Arena, progRoot As Integer, outPath As Pointer) As Integer
  cg_enums = ArenaAlloc(ByRef arena, CgEnumInfo, 128)
  cg_enumMems = ArenaAlloc(ByRef arena, CgEnumMem, 1024)
  cg_structs = ArenaAlloc(ByRef arena, CgStructInfo, 256)
  cg_fields = ArenaAlloc(ByRef arena, CgFieldInfo, 2048)
  cg_globals = ArenaAlloc(ByRef arena, CgGlobalInfo, 1024)
  cg_funcs = ArenaAlloc(ByRef arena, CgFuncInfo, 1024)
  cg_ops = ArenaAlloc(ByRef arena, CgOpInfo, 256)
  cg_params = ArenaAlloc(ByRef arena, CgParamInfo, 4096)
  cg_locals = ArenaAlloc(ByRef arena, CgLocalSym, 512)
  cg_strs = ArenaAlloc(ByRef arena, CgStrConst, 4096)
  cg_staticLocals = ArenaAlloc(ByRef arena, CgStaticLocal, 512)

  cg_enumCount = 0
  cg_enumMemCount = 0
  cg_structCount = 0
  cg_fieldCount = 0
  cg_globalCount = 0
  cg_funcCount = 0
  cg_opCount = 0
  cg_paramCount = 0
  cg_localCount = 0
  cg_strCount = 0
  cg_staticLocalCount = 0
  cg_withDepth = 0
  cg_loopDepth = 0
  cg_lbl = 1
  cg_usesArena = 0
  cg_usesAssert = 0
  cg_usesStrConcat = 0
  cg_usesStrInterp = 0
  cg_usesStrEq = 0
  cg_usesAsync = 0
  cg_blockEnded = 0
  cg_usesPrintf = 0
  cg_usesMalloc = 0
  cg_usesCalloc = 0
  cg_usesRealloc = 0
  cg_usesFree = 0
  cg_usesFopen = 0
  cg_usesGetc = 0
  cg_usesPutc = 0
  cg_inTestFn = 0
  cg_testCount = 0

  Dim defStart As Integer = g_nodes(progRoot).firstChild
  Dim defCount As Integer = g_nodes(progRoot).childCount

  ' Pass 1a: Register all Enums & Struct headers
  For i = 0 To defCount - 1 Do
    Dim d As Integer = g_kids(defStart + i)
    Dim dk As Integer = g_nodes(d).kind
    If dk = NdEnumDef Then
      Dim ms As Integer = cg_enumMemCount
      Dim mc As Integer = g_nodes(d).childCount
      Dim j As Integer = 0
      While j < mc Do
        Dim m As Integer = g_kids(g_nodes(d).firstChild + j)
        cg_enumMems(cg_enumMemCount) = CgEnumMem With (
          namePos = g_nodes(m).intVal,
          nameLen = g_nodes(m).lenVal,
          value = g_nodes(m).op
        )
        cg_enumMemCount = cg_enumMemCount + 1
        j = j + 1
      End While
      Dim isPriv As Integer = 0
      If g_nodes(d).op = 2 Then
        isPriv = 1
      End If
      cg_enums(cg_enumCount) = CgEnumInfo With (
        namePos = g_nodes(d).intVal,
        nameLen = g_nodes(d).lenVal,
        memStart = ms,
        memCount = mc,
        isPrivate = isPriv,
        modId = g_nodes(d).modId
      )
      cg_enumCount = cg_enumCount + 1
    End If
    If dk = NdStructDef Then
      cg_structs(cg_structCount) = CgStructInfo With (
        namePos = g_nodes(d).intVal,
        nameLen = g_nodes(d).lenVal,
        fieldStart = 0,
        fieldCount = 0
      )
      cg_structCount = cg_structCount + 1
    End If
  End For

  ' Pass 1b: Resolve Struct fields (now that all structs & enums are registered)
  Dim si As Integer = 0
  For i = 0 To defCount - 1 Do
    Dim d As Integer = g_kids(defStart + i)
    If g_nodes(d).kind = NdStructDef Then
      Dim fs As Integer = cg_fieldCount
      Dim fc As Integer = g_nodes(d).childCount
      Dim j As Integer = 0
      While j < fc Do
        Dim f As Integer = g_kids(g_nodes(d).firstChild + j)
        cg_fields(cg_fieldCount) = CgFieldInfo With (
          namePos = g_nodes(f).intVal,
          nameLen = g_nodes(f).lenVal,
          ty = CgResolveTypeEx(g_nodes(f).tyKind, g_nodes(f).tyInnerKind, g_nodes(f).tyArrLen, g_nodes(f).tyNamePos, g_nodes(f).tyNameLen)
        )
        cg_fieldCount = cg_fieldCount + 1
        j = j + 1
      End While
      cg_structs(si).fieldStart = fs
      cg_structs(si).fieldCount = fc
      si = si + 1
    End If
  End For

  ' Pass 1c: Register Globals, Functions, and ExternDecls
  For i = 0 To defCount - 1 Do
    Dim d As Integer = g_kids(defStart + i)
    Dim dk As Integer = g_nodes(d).kind
    If dk = NdGlobalVar Then
      Dim gvPub As Integer = 0
      Dim gvPriv As Integer = 0
      Dim gvVol As Integer = 0
      Dim gvHasAddr As Integer = 0
      Dim gvAddr As Integer = 0
      If g_nodes(d).op = 1 Then
        gvPub = 1
      End If
      If g_nodes(d).op = 2 Then
        gvPriv = 1
      End If
      If g_nodes(d).flags = 1 Then
        gvVol = 1
      End If
      If g_nodes(d).right >= 0 Then
        gvHasAddr = 1
        gvAddr = g_nodes(g_nodes(d).right).intVal
      End If
      cg_globals(cg_globalCount) = CgGlobalInfo With (
        namePos = g_nodes(d).intVal,
        nameLen = g_nodes(d).lenVal,
        isPublic = gvPub,
        isPrivate = gvPriv,
        modId = g_nodes(d).modId,
        isVolatile = gvVol,
        hasFixedAddr = gvHasAddr,
        fixedAddr = gvAddr,
        ty = CgResolveTypeEx(g_nodes(d).tyKind, g_nodes(d).tyInnerKind, g_nodes(d).tyArrLen, g_nodes(d).tyNamePos, g_nodes(d).tyNameLen),
        initNode = g_nodes(d).left
      )
      cg_globalCount = cg_globalCount + 1
    End If
    If (dk = NdFuncDef) Or (dk = NdExternDecl) Then
      Dim ps As Integer = cg_paramCount
      Dim pc As Integer = g_nodes(d).extraCount
      Dim j As Integer = 0
      While j < pc Do
        Dim pn As Integer = g_kids(g_nodes(d).extraStart + j)
        cg_params(cg_paramCount) = CgParamInfo With (
          isByRef = g_nodes(pn).op,
          namePos = g_nodes(pn).intVal,
          nameLen = g_nodes(pn).lenVal,
          ty = CgResolveTypeEx(g_nodes(pn).tyKind, g_nodes(pn).tyInnerKind, g_nodes(pn).tyArrLen, g_nodes(pn).tyNamePos, g_nodes(pn).tyNameLen)
        )
        cg_paramCount = cg_paramCount + 1
        j = j + 1
      End While
      Dim isExt = 0, isVar = 0, isPub = 0, isPriv = 0, isAsy = 0 As Integer
      If dk = NdExternDecl Then
        isExt = 1
        isVar = g_nodes(d).op
        isPub = 1
      Else
        If g_nodes(d).op = 1 Then
          isPub = 1
        End If
        If g_nodes(d).op = 2 Then
          isPriv = 1
        End If
        If g_nodes(d).flags = 1 Then
          isAsy = 1
        End If
      End If
      ' Avoid duplicate extern declarations
      cg_curModId = g_nodes(d).modId
      Dim existing As Integer = CgFindFunc(g_nodes(d).intVal, g_nodes(d).lenVal)
      If (isExt = 0) Or (existing < 0) Then
        Dim bRetTy As TypeInfo = CgResolveTypeEx(g_nodes(d).tyKind, g_nodes(d).tyInnerKind, 0, g_nodes(d).tyNamePos, g_nodes(d).tyNameLen)
        Dim cRetTy As TypeInfo = bRetTy
        If isAsy = 1 Then
          cRetTy = TypeInfo With (
            kind = TyTask,
            typeIdx = bRetTy.typeIdx,
            arrLen = 0,
            namePos = bRetTy.namePos,
            nameLen = bRetTy.nameLen,
            innerKind = bRetTy.kind
          )
        End If
        g_nodes(d).third = cg_funcCount
        cg_funcs(cg_funcCount) = CgFuncInfo With (
          namePos = g_nodes(d).intVal,
          nameLen = g_nodes(d).lenVal,
          isExtern = isExt,
          isVariadic = isVar,
          isPublic = isPub,
          isPrivate = isPriv,
          isAsync = isAsy,
          modId = g_nodes(d).modId,
          retTy = cRetTy,
          bodyRetTy = bRetTy,
          paramStart = ps,
          paramCount = pc
        )
        cg_funcCount = cg_funcCount + 1
      End If
    End If
    If dk = NdOpDef Then
      Dim ops As Integer = cg_paramCount
      Dim opc As Integer = g_nodes(d).extraCount
      For j = 0 To opc - 1 Do
        Dim pn As Integer = g_kids(g_nodes(d).extraStart + j)
        cg_params(cg_paramCount) = CgParamInfo With (
          isByRef = g_nodes(pn).op,
          namePos = g_nodes(pn).intVal,
          nameLen = g_nodes(pn).lenVal,
          ty = CgResolveTypeEx(g_nodes(pn).tyKind, g_nodes(pn).tyInnerKind, g_nodes(pn).tyArrLen, g_nodes(pn).tyNamePos, g_nodes(pn).tyNameLen)
        )
        cg_paramCount = cg_paramCount + 1
      End For
      Dim opPub = 0, opPriv = 0 As Integer
      If g_nodes(d).op = 1 Then
        opPub = 1
      End If
      If g_nodes(d).op = 2 Then
        opPriv = 1
      End If
      Dim opRetTy As TypeInfo = CgResolveTypeEx(g_nodes(d).tyKind, g_nodes(d).tyInnerKind, 0, g_nodes(d).tyNamePos, g_nodes(d).tyNameLen)
      g_nodes(d).third = cg_opCount
      cg_ops(cg_opCount) = CgOpInfo With (
        opTok = g_nodes(d).intVal,
        isPublic = opPub,
        isPrivate = opPriv,
        modId = g_nodes(d).modId,
        paramStart = ops,
        paramCount = opc,
        retTy = opRetTy
      )
      cg_opCount = cg_opCount + 1
    End If
  End For

  ' Collect all strings and detect Arena usage
  CgCollectStrings(progRoot)

  cg_out = Fopen(outPath, "w")
  If IsNull(cg_out) Then
    Printf(MSG_CG_OPEN_FAIL)
    Return -1
  End If

  fputs(IR_MODULE_HEADER, cg_out)
  Match cg_target With
    Case TgtLinuxArm64 Then
      fputs(IR_DL_LINUX_ARM64, cg_out)
      fputs(IR_TRIPLE_LINUX_ARM64, cg_out)
    Case TgtMacosArm64 Then
      fputs(IR_DL_MACOS_ARM64, cg_out)
      fputs(IR_TRIPLE_MACOS_ARM64, cg_out)
    Case TgtWindowsX64 Then
      fputs(IR_DL_WINDOWS_X64, cg_out)
      fputs(IR_TRIPLE_WINDOWS_X64, cg_out)
    Case TgtArmM Then
      fputs(IR_DL_ARM_M, cg_out)
      fputs(IR_TRIPLE_ARM_M, cg_out)
    Case _ Then
      fputs(IR_DL_LINUX_X64, cg_out)
      fputs(IR_TRIPLE_LINUX_X64, cg_out)
  End Match

  If cg_usesArena = 1 Then
    fputs(IR_ARENA_STRUCT_DEF, cg_out)
  End If

  ' Emit struct layouts
  For i = 0 To cg_structCount - 1 Do
    fprintf(cg_out, "%%struct.")
    CgEmitSlice(cg_structs(i).namePos, cg_structs(i).nameLen)
    fprintf(cg_out, " = type { ")
    Dim j As Integer = 0
    While j < cg_structs(i).fieldCount Do
      If j > 0 Then
        fprintf(cg_out, ", ")
      End If
      CgEmitLlvmType(cg_fields(cg_structs(i).fieldStart + j).ty)
      j = j + 1
    End While
    fprintf(cg_out, " }\n")
  End For
  ' Emit Async frame struct layouts
  For i = 0 To cg_funcCount - 1 Do
    If cg_funcs(i).isAsync = 1 Then
      fprintf(cg_out, "%%struct.__vb_async_")
      CgEmitSymName(cg_funcs(i).namePos, cg_funcs(i).nameLen, cg_funcs(i).modId, cg_funcs(i).isPrivate)
      fprintf(cg_out, " = type { i64, ptr")
      If cg_funcs(i).bodyRetTy.kind <> TyNothing Then
        fprintf(cg_out, ", ")
        CgEmitLlvmType(cg_funcs(i).bodyRetTy)
      End If
      For aj = 0 To cg_funcs(i).paramCount - 1 Do
        Dim ap As CgParamInfo = cg_params(cg_funcs(i).paramStart + aj)
        fprintf(cg_out, ", ")
        If (ap.isByRef = 1) Or (ap.ty.arrLen < 0) Then
          fprintf(cg_out, "ptr")
        Else
          CgEmitLlvmType(ap.ty)
        End If
      End For
      fprintf(cg_out, " }\n")
    End If
  End For
  fprintf(cg_out, "\n")

  ' Emit Static globals (skip fixed-address MMIO globals which lower to inttoptr)
  For i = 0 To cg_globalCount - 1 Do
    If cg_globals(i).hasFixedAddr = 0 Then
      fprintf(cg_out, "@")
      CgEmitSymName(cg_globals(i).namePos, cg_globals(i).nameLen, cg_globals(i).modId, cg_globals(i).isPrivate)
      If cg_globals(i).isPublic = 1 Then
        fprintf(cg_out, " = global ")
      Else
        fprintf(cg_out, " = internal global ")
      End If
      CgEmitLlvmType(cg_globals(i).ty)
      fprintf(cg_out, " ")
      CgEmitConstInit(cg_globals(i).ty, cg_globals(i).initNode)
      fprintf(cg_out, ", align %d\n", CgTypeAlign(cg_globals(i).ty))
    End If
  End For
  fprintf(cg_out, "\n")

  ' Emit function-local Static variables
  For i = 0 To cg_staticLocalCount - 1 Do
    fprintf(cg_out, "@__vb_static_%d = internal global ", i)
    CgEmitLlvmType(cg_staticLocals(i).ty)
    fprintf(cg_out, " ")
    CgEmitConstInit(cg_staticLocals(i).ty, cg_staticLocals(i).initNode)
    fprintf(cg_out, ", align 8\n")
  End For
  fprintf(cg_out, "\n")

  ' Emit string literal globals
  CgEmitStringGlobals()
  Dim interpFmtStart As Integer = cg_strCount
  If cg_usesStrInterp = 1 Then
    fputs(IR_INTERP_STR_CONSTS, cg_out)
  End If
  fprintf(cg_out, "\n")

  ' Emit default C runtime declarations (skipping any overridden by Declare, and unused ones in freestanding mode)
  If cg_usesArena = 1 Then
    cg_usesCalloc = 1
    cg_usesFree = 1
  End If
  If ((cg_isFreestanding = 0) Or (cg_usesPrintf = 1)) And (Not CgHasExtern("printf", 6)) Then
    fprintf(cg_out, "declare i32 @printf(ptr, ...)\n")
  End If
  If ((cg_isFreestanding = 0) Or (cg_usesMalloc = 1)) And (Not CgHasExtern("malloc", 6)) Then
    fprintf(cg_out, "declare ptr @malloc(i32)\n")
  End If
  If ((cg_isFreestanding = 0) Or (cg_usesCalloc = 1)) And (Not CgHasExtern("calloc", 6)) Then
    fprintf(cg_out, "declare ptr @calloc(i64, i64)\n")
  End If
  If ((cg_isFreestanding = 0) Or (cg_usesRealloc = 1)) And (Not CgHasExtern("realloc", 7)) Then
    fprintf(cg_out, "declare ptr @realloc(ptr, i64)\n")
  End If
  If ((cg_isFreestanding = 0) Or (cg_usesFree = 1)) And (Not CgHasExtern("free", 4)) Then
    fprintf(cg_out, "declare void @free(ptr)\n")
  End If
  If ((cg_isFreestanding = 0) Or (cg_usesFopen = 1)) And (Not CgHasExtern("fopen", 5)) Then
    fprintf(cg_out, "declare ptr @fopen(ptr, ptr)\n")
  End If
  If ((cg_isFreestanding = 0) Or (cg_usesGetc = 1)) And (Not CgHasExtern("getc", 4)) Then
    fprintf(cg_out, "declare i32 @getc(ptr)\n")
  End If
  If ((cg_isFreestanding = 0) Or (cg_usesPutc = 1)) And (Not CgHasExtern("putc", 4)) Then
    fprintf(cg_out, "declare i32 @putc(i32, ptr)\n")
  End If

  ' Emit user Declare externs
  For i = 0 To cg_funcCount - 1 Do
    If cg_funcs(i).isExtern = 1 Then
      fprintf(cg_out, "declare ")
      CgEmitExternRetType(cg_funcs(i).retTy)
      fprintf(cg_out, " @")
      CgEmitSymName(cg_funcs(i).namePos, cg_funcs(i).nameLen, 0, 0)
      fprintf(cg_out, "(")
      For j = 0 To cg_funcs(i).paramCount - 1 Do
        If j > 0 Then
          fprintf(cg_out, ", ")
        End If
        CgEmitExternParamType(cg_params(cg_funcs(i).paramStart + j))
      End For
      If cg_funcs(i).isVariadic = 1 Then
        If cg_funcs(i).paramCount > 0 Then
          fprintf(cg_out, ", ...")
        Else
          fprintf(cg_out, "...")
        End If
      End If
      fprintf(cg_out, ")\n")
    End If
  End For

  If (cg_usesAsync = 1) And (cg_isFreestanding = 0) Then
    If Not CgHasExtern("pthread_create", 14) Then
      fprintf(cg_out, "declare i32 @pthread_create(ptr, ptr, ptr, ptr)\n")
    End If
    If Not CgHasExtern("pthread_join", 12) Then
      fprintf(cg_out, "declare i32 @pthread_join(i64, ptr)\n")
    End If
  End If

  If (cg_usesStrInterp = 1) And (Not CgHasExtern("snprintf", 8)) Then
    If cg_ptrSize = 4 Then
      fputs("declare i32 @snprintf(ptr, i32, ptr, ...)\n", cg_out)
    Else
      fputs("declare i32 @snprintf(ptr, i64, ptr, ...)\n", cg_out)
    End If
  End If

  fputs(IR_MEMCPY_DECL, cg_out)
  If cg_usesArena = 1 Then
    CgEmitArenaRuntime()
  End If
  CgEmitAssertRuntime()

  ' Emit all function and operator bodies
  For i = 0 To defCount - 1 Do
    Dim d As Integer = g_kids(defStart + i)
    If g_nodes(d).kind = NdFuncDef Then
      CgEmitFunc(d, g_nodes(d).third)
    End If
    If g_nodes(d).kind = NdOpDef Then
      CgEmitOpDef(d, g_nodes(d).third)
    End If
  End For

  If (cg_testMode = 1) And (cg_testCount > 0) Then
    Dim tIdx As Integer = 0
    For i = 0 To defCount - 1 Do
      Dim d As Integer = g_kids(defStart + i)
      If g_nodes(d).kind = NdTestDef Then
        CgEmitTestFn(d, tIdx)
        tIdx = tIdx + 1
      End If
    End For
    CgEmitTestMain(progRoot)
  End If

  If cg_strCount > interpFmtStart Then
    CgEmitStringGlobalsRange(interpFmtStart, cg_strCount)
    fprintf(cg_out, "\n")
  End If

  If cg_usesStrConcat = 1 Then
    CgEmitStrConcatRuntime()
  End If

  If cg_usesStrEq = 1 Then
    CgEmitStrEqRuntime()
  End If

  fclose(cg_out)
  Return 0
End Function
