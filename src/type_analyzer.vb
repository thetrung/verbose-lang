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
  Dim namePos, nameLen, isExtern, isVariadic, isPrivate, modId, paramStart, paramCount As Integer
  Dim retTy As TypeInfo
End Structure

Structure TaLocalSym
  Dim namePos, nameLen As Integer, ty As TypeInfo
End Structure

Private Static ta_enums() As TaEnumInfo, ta_enumMems() As TaEnumMem
Private Static ta_structs() As TaStructInfo, ta_fields() As TaFieldInfo
Private Static ta_globals() As TaGlobalInfo, ta_funcs() As TaFuncInfo
Private Static ta_params() As TaParamInfo, ta_locals() As TaLocalSym
Private Static ta_enumCount, ta_enumMemCount, ta_structCount, ta_fieldCount, ta_globalCount, ta_funcCount, ta_paramCount, ta_localCount, ta_curModId, ta_errCount As Integer = 0
Private Static ta_curRetTy As TypeInfo

Private Sub TaPrintSlice(pos, slen As Integer)
  Dim i As Integer = 0
  While i < slen Do
    Dim ch As Integer = g_srcPool(pos + i)
    Printf("%c", ch)
    i = i + 1
  End While
End Sub

Private Sub TaPrintType(ty As TypeInfo)
  Select Case ty.kind
    Case TypeKind.TyNothing
      Printf("Nothing")
    Case TypeKind.TyInt
      Printf("Integer")
    Case TypeKind.TyByte
      Printf("Byte")
    Case TypeKind.TyShort
      Printf("Short")
    Case TypeKind.TyLong
      Printf("Long")
    Case TypeKind.TySingle
      Printf("Single")
    Case TypeKind.TyDouble
      Printf("Double")
    Case TypeKind.TyBoolean
      Printf("Boolean")
    Case TypeKind.TyPointer
      Printf("Pointer")
    Case TypeKind.TyArena
      Printf("Arena")
    Case TypeKind.TyStruct, TypeKind.TyEnum
      TaPrintSlice(ty.namePos, ty.nameLen)
    Case Else
      Printf("Unknown")
  End Select
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
  Dim i As Integer = 0
  While i < ta_enumCount Do
    If (ta_enums(i).isPrivate = 1) And (ta_enums(i).modId = ta_curModId) And SlicesEqual(ta_enums(i).namePos, ta_enums(i).nameLen, pos, slen) Then
      Return i
    End If
    i = i + 1
  End While
  i = 0
  While i < ta_enumCount Do
    If (ta_enums(i).isPrivate = 0) And SlicesEqual(ta_enums(i).namePos, ta_enums(i).nameLen, pos, slen) Then
      Return i
    End If
    i = i + 1
  End While
  Return -1
End Function

Private Function TaHasPrivateEnumOther(pos, slen As Integer) As Boolean
  Dim i As Integer = 0
  While i < ta_enumCount Do
    If (ta_enums(i).isPrivate = 1) And (ta_enums(i).modId <> ta_curModId) And SlicesEqual(ta_enums(i).namePos, ta_enums(i).nameLen, pos, slen) Then
      Return True
    End If
    i = i + 1
  End While
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

Private Function TaFindStruct(pos, slen As Integer) As Integer
  Dim i As Integer = 0
  While i < ta_structCount Do
    If (ta_structs(i).isPrivate = 1) And (ta_structs(i).modId = ta_curModId) And SlicesEqual(ta_structs(i).namePos, ta_structs(i).nameLen, pos, slen) Then
      Return i
    End If
    i = i + 1
  End While
  i = 0
  While i < ta_structCount Do
    If (ta_structs(i).isPrivate = 0) And SlicesEqual(ta_structs(i).namePos, ta_structs(i).nameLen, pos, slen) Then
      Return i
    End If
    i = i + 1
  End While
  Return -1
End Function

Private Function TaHasPrivateStructOther(pos, slen As Integer) As Boolean
  Dim i As Integer = 0
  While i < ta_structCount Do
    If (ta_structs(i).isPrivate = 1) And (ta_structs(i).modId <> ta_curModId) And SlicesEqual(ta_structs(i).namePos, ta_structs(i).nameLen, pos, slen) Then
      Return True
    End If
    i = i + 1
  End While
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
  Dim i As Integer = 0
  While i < ta_globalCount Do
    If (ta_globals(i).isPrivate = 1) And (ta_globals(i).modId = ta_curModId) And SlicesEqual(ta_globals(i).namePos, ta_globals(i).nameLen, pos, slen) Then
      Return i
    End If
    i = i + 1
  End While
  i = 0
  While i < ta_globalCount Do
    If (ta_globals(i).isPrivate = 0) And SlicesEqual(ta_globals(i).namePos, ta_globals(i).nameLen, pos, slen) Then
      Return i
    End If
    i = i + 1
  End While
  Return -1
End Function

Private Function TaHasPrivateGlobalOther(pos, slen As Integer) As Boolean
  Dim i As Integer = 0
  While i < ta_globalCount Do
    If (ta_globals(i).isPrivate = 1) And (ta_globals(i).modId <> ta_curModId) And SlicesEqual(ta_globals(i).namePos, ta_globals(i).nameLen, pos, slen) Then
      Return True
    End If
    i = i + 1
  End While
  Return False
End Function

Private Function TaFindFunc(pos, slen As Integer) As Integer
  Dim i As Integer = 0
  While i < ta_funcCount Do
    If (ta_funcs(i).isPrivate = 1) And (ta_funcs(i).modId = ta_curModId) And SlicesEqual(ta_funcs(i).namePos, ta_funcs(i).nameLen, pos, slen) Then
      Return i
    End If
    i = i + 1
  End While
  i = 0
  While i < ta_funcCount Do
    If (ta_funcs(i).isPrivate = 0) And SlicesEqual(ta_funcs(i).namePos, ta_funcs(i).nameLen, pos, slen) Then
      Return i
    End If
    i = i + 1
  End While
  Return -1
End Function

Private Function TaHasPrivateFuncOther(pos, slen As Integer) As Boolean
  Dim i As Integer = 0
  While i < ta_funcCount Do
    If (ta_funcs(i).isPrivate = 1) And (ta_funcs(i).modId <> ta_curModId) And SlicesEqual(ta_funcs(i).namePos, ta_funcs(i).nameLen, pos, slen) Then
      Return True
    End If
    i = i + 1
  End While
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

Private Function TaResolveType(rawKind As Integer, arrLen As Integer, nPos As Integer, nLen As Integer) As TypeInfo
  Dim res As TypeInfo With (
    kind = rawKind,
    typeIdx = -1,
    arrLen = arrLen,
    namePos = nPos,
    nameLen = nLen
  )
  If rawKind = TypeKind.TyStruct Then
    Dim eIdx As Integer = TaFindEnum(nPos, nLen)
    If eIdx >= 0 Then
      res.kind = TypeKind.TyEnum
      res.typeIdx = eIdx
    Else
      Dim sIdx As Integer = TaFindStruct(nPos, nLen)
      res.typeIdx = sIdx
    End If
  End If
  Return res
End Function

Private Sub TaValidateType(ty As TypeInfo)
  If ty.arrLen = -2 Then
    Printf(MSG_TA_ARRAY_SIZE_POS)
    ta_errCount = ta_errCount + 1
  End If
  If (ty.kind = TypeKind.TyStruct) And (ty.typeIdx < 0) Then
    If TaHasPrivateStructOther(ty.namePos, ty.nameLen) Or TaHasPrivateEnumOther(ty.namePos, ty.nameLen) Then
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
  If (t.arrLen = 0) And (t.kind = TypeKind.TyPointer) Then
    Return True
  End If
  If (t.arrLen <> 0) And (t.kind = TypeKind.TyByte) Then
    Return True
  End If
  Return False
End Function

Private Function TaNormKind(k As Integer) As Integer
  If k = TypeKind.TyEnum Then
    Return TypeKind.TyInt
  End If
  Return k
End Function

Private Function TaIsIntType(ty As TypeInfo) As Boolean
  If ty.arrLen <> 0 Then
    Return False
  End If
  Dim k As Integer = TaNormKind(ty.kind)
  Return (k = TypeKind.TyByte) Or (k = TypeKind.TyShort) Or (k = TypeKind.TyInt) Or (k = TypeKind.TyLong)
End Function

Private Function TaIsFloatType(ty As TypeInfo) As Boolean
  If ty.arrLen <> 0 Then
    Return False
  End If
  Return (ty.kind = TypeKind.TySingle) Or (ty.kind = TypeKind.TyDouble)
End Function

Private Function TaBaseTypesEqual(t1 As TypeInfo, t2 As TypeInfo) As Boolean
  Dim k1 As Integer = TaNormKind(t1.kind)
  Dim k2 As Integer = TaNormKind(t2.kind)
  If k1 <> k2 Then
    Return False
  End If
  If k1 = TypeKind.TyStruct Then
    Return t1.typeIdx = t2.typeIdx
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
  ' Pointer <- Array or Integer
  If (target.arrLen = 0) And (target.kind = TypeKind.TyPointer) Then
    If actual.arrLen <> 0 Then
      Return True
    End If
    If TaIsIntType(actual) Then
      Return True
    End If
  End If
  ' Unsized slice <- Pointer, same-element Array, or literal 0
  If target.arrLen = -1 Then
    If (actual.arrLen = 0) And (actual.kind = TypeKind.TyPointer) Then
      Return True
    End If
    If (actual.arrLen <> 0) And TaBaseTypesEqual(target, actual) Then
      Return True
    End If
    If (rhsNode >= 0) And (g_nodes(rhsNode).kind = NodeKind.NdIntLit) And (g_nodes(rhsNode).intVal = 0) Then
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
  If k = NodeKind.NdIntLit Then
    Return True
  End If
  If (k = NodeKind.NdNeg) And (g_nodes(id).left >= 0) Then
    Return g_nodes(g_nodes(id).left).kind = NodeKind.NdIntLit
  End If
  Return False
End Function

Private Function TaIsFloatLitNode(id As Integer) As Boolean
  If id < 0 Then
    Return False
  End If
  Dim k As Integer = g_nodes(id).kind
  If k = NodeKind.NdFloatLit Then
    Return True
  End If
  If (k = NodeKind.NdNeg) And (g_nodes(id).left >= 0) Then
    Return g_nodes(g_nodes(id).left).kind = NodeKind.NdFloatLit
  End If
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
  If (vTy.arrLen <> 0) Or (vTy.kind = TypeKind.TyPointer) Then
    Return True
  End If
  If (TaFindFunc(nPos, nLen) < 0) And (TaFindStruct(nPos, nLen) < 0) Then
    Return True
  End If
  Return False
End Function

Private Function TaIsLValue(id As Integer) As Boolean
  If id < 0 Then
    Return False
  End If
  Dim k As Integer = g_nodes(id).kind
  If (k = NodeKind.NdVar) Or (k = NodeKind.NdFieldAccess) Or (k = NodeKind.NdIndexAccess) Then
    Return True
  End If
  If k = NodeKind.NdCall Then
    Return TaIsVarIndexCall(id)
  End If
  Return False
End Function

Private Function TaIsConstExpr(ty As TypeInfo, exprId As Integer) As Boolean
  If exprId < 0 Then
    Return True
  End If
  Dim k As Integer = g_nodes(exprId).kind
  ' Pointer or unsized slice initialized to 0 or StringLit
  If ((ty.arrLen = 0) And (ty.kind = TypeKind.TyPointer)) Or (ty.arrLen = -1) Then
    If (k = NodeKind.NdIntLit) And (g_nodes(exprId).intVal = 0) Then
      Return True
    End If
    If (ty.arrLen = 0) And (k = NodeKind.NdStrLit) Then
      Return True
    End If
    Return False
  End If
  If TaIsIntType(ty) Then
    If TaIsIntLitNode(exprId) Then
      Return True
    End If
    If k = NodeKind.NdFieldAccess Then
      Dim baseId As Integer = g_nodes(exprId).left
      If (baseId >= 0) And (g_nodes(baseId).kind = NodeKind.NdVar) Then
        Dim eIdx As Integer = TaFindEnum(g_nodes(baseId).intVal, g_nodes(baseId).lenVal)
        If (eIdx >= 0) And TaHasEnumMem(eIdx, g_nodes(exprId).intVal, g_nodes(exprId).lenVal) Then
          Return True
        End If
      End If
    End If
    Return False
  End If
  If TaIsFloatType(ty) Then
    Return TaIsFloatLitNode(exprId)
  End If
  If (ty.arrLen = 0) And (ty.kind = TypeKind.TyBoolean) Then
    Return k = NodeKind.NdBoolLit
  End If
  If (ty.arrLen > 0) And (k = NodeKind.NdArrayLit) Then
    If g_nodes(exprId).childCount > ty.arrLen Then
      Printf("Semantic Type Error: Static array initializer has %d elements, exceeding size %d\n", g_nodes(exprId).childCount, ty.arrLen)
      ta_errCount = ta_errCount + 1
      Return True
    End If
    Dim elemTy As TypeInfo With ( kind = ty.kind, typeIdx = ty.typeIdx, arrLen = 0, namePos = ty.namePos, nameLen = ty.nameLen )
    Dim i As Integer = 0
    While i < g_nodes(exprId).childCount Do
      If Not TaIsConstExpr(elemTy, g_kids(g_nodes(exprId).firstChild + i)) Then
        Return False
      End If
      i = i + 1
    End While
    Return True
  End If
  If (ty.arrLen = 0) And (ty.kind = TypeKind.TyStruct) And (ty.typeIdx >= 0) And (k = NodeKind.NdStructInit) Then
    Dim initSIdx As Integer = TaFindStruct(g_nodes(exprId).intVal, g_nodes(exprId).lenVal)
    If initSIdx <> ty.typeIdx Then
      Printf("Semantic Type Error: Static struct type mismatch\n")
      ta_errCount = ta_errCount + 1
      Return True
    End If
    Dim i As Integer = 0
    While i < g_nodes(exprId).childCount Do
      Dim fi As Integer = g_kids(g_nodes(exprId).firstChild + i)
      Dim fTy As TypeInfo
      Dim fIdx As Integer = TaFindStructField(ty.typeIdx, g_nodes(fi).intVal, g_nodes(fi).lenVal, ByRef fTy)
      If fIdx >= 0 Then
        If Not TaIsConstExpr(fTy, g_nodes(fi).left) Then
          Return False
        End If
      End If
      i = i + 1
    End While
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
  If (dt.arrLen <> 0) Or (dt.kind <> TypeKind.TyArena) Then
    Printf("Semantic Type Error: %s argument must be an Arena variable\n", fnName)
    ta_errCount = ta_errCount + 1
    Return False
  End If
  Return True
End Function

Private Function TaCheckIndex(baseTy As TypeInfo, idxExpr As Integer) As TypeInfo
  Dim idxTy As TypeInfo = TaTypeOfExpr(idxExpr)
  If Not TaIsIntType(idxTy) Then
    Printf("Semantic Type Error: Array index must be an integer type\n")
    ta_errCount = ta_errCount + 1
  End If
  If baseTy.arrLen <> 0 Then
    Dim elemTy As TypeInfo With ( kind = baseTy.kind, typeIdx = baseTy.typeIdx, arrLen = 0, namePos = baseTy.namePos, nameLen = baseTy.nameLen )
    Return elemTy
  End If
  If baseTy.kind = TypeKind.TyPointer Then
    Return TaMakePrim(TypeKind.TyByte)
  End If
  Printf("Semantic Type Error: Cannot index non-array, non-pointer expression\n")
  ta_errCount = ta_errCount + 1
  Return TaMakePrim(TypeKind.TyInt)
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
      Return TaMakePrim(TypeKind.TyBoolean)
    End If
    Dim a0 As Integer = g_kids(aStart)
    If g_nodes(a0).op = 1 Then
      Printf("Semantic Type Error: IsNull() expects exactly one ByVal pointer/slice argument\n")
      ta_errCount = ta_errCount + 1
      Return TaMakePrim(TypeKind.TyBoolean)
    End If
    Dim pt As TypeInfo = TaTypeOfExpr(g_nodes(a0).left)
    If (pt.arrLen = 0) And (pt.kind <> TypeKind.TyPointer) Then
      Printf("Semantic Type Error: IsNull expects a Pointer or slice argument\n")
      ta_errCount = ta_errCount + 1
    End If
    Return TaMakePrim(TypeKind.TyBoolean)
  End If

  ' Len(arr)
  If SliceEqualsStr(nPos, nLen, "Len", 3) Then
    If aCount <> 1 Then
      Printf("Semantic Type Error: Len() expects exactly one ByVal fixed-size array argument\n")
      ta_errCount = ta_errCount + 1
      Return TaMakePrim(TypeKind.TyInt)
    End If
    Dim a0 As Integer = g_kids(aStart)
    If (g_nodes(a0).op = 1) Or (Not TaIsLValue(g_nodes(a0).left)) Then
      Printf("Semantic Type Error: Len() requires a fixed-size array\n")
      ta_errCount = ta_errCount + 1
      Return TaMakePrim(TypeKind.TyInt)
    End If
    Dim aty As TypeInfo = TaTypeOfExpr(g_nodes(a0).left)
    If aty.arrLen <= 0 Then
      Printf("Semantic Type Error: Len() requires a fixed-size array\n")
      ta_errCount = ta_errCount + 1
    End If
    Return TaMakePrim(TypeKind.TyInt)
  End If

  ' ArenaCreate(cap)
  If SliceEqualsStr(nPos, nLen, "ArenaCreate", 11) Then
    If aCount <> 1 Then
      Printf("Semantic Type Error: ArenaCreate() expects exactly one ByVal capacity argument\n")
      ta_errCount = ta_errCount + 1
      Return TaMakePrim(TypeKind.TyArena)
    End If
    Dim a0 As Integer = g_kids(aStart)
    If g_nodes(a0).op = 1 Then
      Printf("Semantic Type Error: ArenaCreate() expects exactly one ByVal capacity argument\n")
      ta_errCount = ta_errCount + 1
      Return TaMakePrim(TypeKind.TyArena)
    End If
    Dim ct As TypeInfo = TaTypeOfExpr(g_nodes(a0).left)
    If Not TaIsIntType(ct) Then
      Printf("Semantic Type Error: ArenaCreate capacity must be an integer type\n")
      ta_errCount = ta_errCount + 1
    End If
    Return TaMakePrim(TypeKind.TyArena)
  End If

  ' ArenaFromBuffer(ByRef buf, cap)
  If SliceEqualsStr(nPos, nLen, "ArenaFromBuffer", 15) Then
    If aCount <> 2 Then
      Printf("Semantic Type Error: ArenaFromBuffer() expects (ByRef buffer, capacity)\n")
      ta_errCount = ta_errCount + 1
      Return TaMakePrim(TypeKind.TyArena)
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
    Return TaMakePrim(TypeKind.TyArena)
  End If

  ' ArenaAllocBytes(ByRef arena, bytes)
  If SliceEqualsStr(nPos, nLen, "ArenaAllocBytes", 15) Then
    If aCount <> 2 Then
      Printf("Semantic Type Error: ArenaAllocBytes() expects (ByRef arena, byteCount)\n")
      ta_errCount = ta_errCount + 1
      Return TaMakePrim(TypeKind.TyPointer)
    End If
    Dim a0 As Integer = g_kids(aStart)
    Dim a1 As Integer = g_kids(aStart + 1)
    TaCheckArenaArg("ArenaAllocBytes", g_nodes(a0).op, g_nodes(a0).left)
    Dim bt As TypeInfo = TaTypeOfExpr(g_nodes(a1).left)
    If Not TaIsIntType(bt) Then
      Printf("Semantic Type Error: ArenaAllocBytes size must be an integer type\n")
      ta_errCount = ta_errCount + 1
    End If
    Return TaMakePrim(TypeKind.TyPointer)
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
    Return TaMakePrim(TypeKind.TyNothing)
  End If

  If SliceEqualsStr(nPos, nLen, "ArenaDestroy", 12) Then
    If aCount <> 1 Then
      Printf("Semantic Type Error: ArenaDestroy() expects (ByRef arena)\n")
      ta_errCount = ta_errCount + 1
    Else
      Dim a0 As Integer = g_kids(aStart)
      TaCheckArenaArg("ArenaDestroy", g_nodes(a0).op, g_nodes(a0).left)
    End If
    Return TaMakePrim(TypeKind.TyNothing)
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
    Return TaMakePrim(TypeKind.TyLong)
  End If

  If SliceEqualsStr(nPos, nLen, "ArenaUsed", 9) Then
    If aCount <> 1 Then
      Printf("Semantic Type Error: ArenaUsed() expects (ByRef arena)\n")
      ta_errCount = ta_errCount + 1
    Else
      Dim a0 As Integer = g_kids(aStart)
      TaCheckArenaArg("ArenaUsed", g_nodes(a0).op, g_nodes(a0).left)
    End If
    Return TaMakePrim(TypeKind.TyLong)
  End If

  If SliceEqualsStr(nPos, nLen, "ArenaRemaining", 14) Then
    If aCount <> 1 Then
      Printf("Semantic Type Error: ArenaRemaining() expects (ByRef arena)\n")
      ta_errCount = ta_errCount + 1
    Else
      Dim a0 As Integer = g_kids(aStart)
      TaCheckArenaArg("ArenaRemaining", g_nodes(a0).op, g_nodes(a0).left)
    End If
    Return TaMakePrim(TypeKind.TyLong)
  End If

  If SliceEqualsStr(nPos, nLen, "ArenaCapacity", 13) Then
    If aCount <> 1 Then
      Printf("Semantic Type Error: ArenaCapacity() expects (ByRef arena)\n")
      ta_errCount = ta_errCount + 1
    Else
      Dim a0 As Integer = g_kids(aStart)
      TaCheckArenaArg("ArenaCapacity", g_nodes(a0).op, g_nodes(a0).left)
    End If
    Return TaMakePrim(TypeKind.TyLong)
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
    Return TaMakePrim(TypeKind.TyNothing)
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
    Return TaMakePrim(TypeKind.TyPointer)
  End If

  ' HeapFree(ptr)
  If SliceEqualsStr(nPos, nLen, "HeapFree", 8) Then
    If aCount <> 1 Then
      Printf("Semantic Type Error: HeapFree() expects exactly one ByVal pointer/array argument\n")
      ta_errCount = ta_errCount + 1
    Else
      Dim a0 As Integer = g_kids(aStart)
      Dim pt As TypeInfo = TaTypeOfExpr(g_nodes(a0).left)
      If (pt.arrLen = 0) And (pt.kind <> TypeKind.TyPointer) Then
        Printf("Semantic Type Error: HeapFree expects a Pointer or heap array\n")
        ta_errCount = ta_errCount + 1
      End If
    End If
    Return TaMakePrim(TypeKind.TyNothing)
  End If

  ' Printf(...)
  If SliceEqualsStr(nPos, nLen, "Printf", 6) Then
    Dim i As Integer = 0
    While i < aCount Do
      Dim aNode As Integer = g_kids(aStart + i)
      If g_nodes(aNode).op = 1 Then
        Printf("Semantic Type Error: Printf does not accept ByRef arguments\n")
        ta_errCount = ta_errCount + 1
      End If
      TaTypeOfExpr(g_nodes(aNode).left)
      i = i + 1
    End While
    Return TaMakePrim(TypeKind.TyInt)
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
    Dim res As TypeInfo With ( kind = TypeKind.TyStruct, typeIdx = sIdx, arrLen = 0, namePos = nPos, nameLen = nLen )
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
    Dim ai As Integer = 0
    While ai < aCount Do
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
      ai = ai + 1
    End While
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
    Return TaMakePrim(TypeKind.TyPointer)
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
    Return TaMakePrim(TypeKind.TyNothing)
  End If

  If SliceEqualsStr(nPos, nLen, "Getc", 4) Or SliceEqualsStr(nPos, nLen, "Putc", 4) Then
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
    Return TaMakePrim(TypeKind.TyInt)
  End If

  If TaHasPrivateFuncOther(nPos, nLen) Then
    Printf("Semantic Type Error: Cannot access Private function '")
    TaPrintSlice(nPos, nLen)
    Printf("' from included module\n")
    ta_errCount = ta_errCount + 1
    Return TaMakePrim(TypeKind.TyInt)
  End If

  Printf("Semantic Type Error: Undefined function or constructor: ")
  TaPrintSlice(nPos, nLen)
  Printf("\n")
  ta_errCount = ta_errCount + 1
  Return TaMakePrim(TypeKind.TyInt)
End Function

Private Function TaTypeOfExpr(id As Integer) As TypeInfo
  If id < 0 Then
    Return TaMakePrim(TypeKind.TyInt)
  End If
  Dim k As Integer = g_nodes(id).kind

  If k = NodeKind.NdIntLit Then
    Return TaMakePrim(TypeKind.TyInt)
  End If
  If k = NodeKind.NdFloatLit Then
    Return TaMakePrim(TypeKind.TyDouble)
  End If
  If k = NodeKind.NdBoolLit Then
    Return TaMakePrim(TypeKind.TyBoolean)
  End If
  If k = NodeKind.NdStrLit Then
    Return TaMakePrim(TypeKind.TyPointer)
  End If

  If k = NodeKind.NdVar Then
    Dim nPos As Integer = g_nodes(id).intVal
    Dim nLen As Integer = g_nodes(id).lenVal
    Dim vTy As TypeInfo
    If TaLookupVar(nPos, nLen, ByRef vTy) Then
      Return vTy
    End If
    Dim eIdx As Integer = TaFindEnum(nPos, nLen)
    If eIdx >= 0 Then
      Dim et As TypeInfo With ( kind = TypeKind.TyEnum, typeIdx = eIdx, arrLen = 0, namePos = nPos, nameLen = nLen )
      Return et
    End If
    If TaHasPrivateGlobalOther(nPos, nLen) Or TaHasPrivateFuncOther(nPos, nLen) Then
      Printf("Semantic Type Error: Cannot access Private symbol '")
      TaPrintSlice(nPos, nLen)
      Printf("' from included module\n")
      ta_errCount = ta_errCount + 1
      Return TaMakePrim(TypeKind.TyInt)
    End If
    Printf("Semantic Type Error: Undefined variable or constant reference: ")
    TaPrintSlice(nPos, nLen)
    Printf("\n")
    ta_errCount = ta_errCount + 1
    Return TaMakePrim(TypeKind.TyInt)
  End If

  If k = NodeKind.NdNot Then
    Dim t As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
    If ((t.arrLen = 0) And (t.kind = TypeKind.TyBoolean)) Or TaIsIntType(t) Then
      Return t
    End If
    Printf("Semantic Type Error: Invalid unary operation 'Not'\n")
    ta_errCount = ta_errCount + 1
    Return t
  End If

  If k = NodeKind.NdNeg Then
    Dim t As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
    If TaIsIntType(t) Or TaIsFloatType(t) Then
      Return t
    End If
    Printf("Semantic Type Error: Invalid unary operation 'Neg'\n")
    ta_errCount = ta_errCount + 1
    Return t
  End If

  If k = NodeKind.NdBinOp Then
    Dim e1 As Integer = g_nodes(id).left
    Dim e2 As Integer = g_nodes(id).right
    Dim op As Integer = g_nodes(id).op
    Dim t1 As TypeInfo = TaTypeOfExpr(e1)
    Dim t2 As TypeInfo = TaTypeOfExpr(e2)
    If op = TokenKind.TkAmp Then
      If TaIsStrLike(t1) And TaIsStrLike(t2) Then
        Return TaMakePrim(TypeKind.TyPointer)
      End If
      Printf(MSG_TA_STR_CONCAT_ERR)
      ta_errCount = ta_errCount + 1
      Return TaMakePrim(TypeKind.TyPointer)
    End If
    If (op = TokenKind.TkPlus) And TaIsStrLike(t1) And TaIsStrLike(t2) Then
      Return TaMakePrim(TypeKind.TyPointer)
    End If
    Dim unified As TypeInfo = t1
    Dim okUnify As Boolean = False
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
    Dim isCmp As Boolean = (op = TokenKind.TkEq) Or (op = TokenKind.TkNeq) Or (op = TokenKind.TkLt) Or (op = TokenKind.TkGt) Or (op = TokenKind.TkLe) Or (op = TokenKind.TkGe)
    If TaIsFloatType(unified) Then
      If (op = TokenKind.TkAnd) Or (op = TokenKind.TkOr) Or (op = TokenKind.TkXor) Or (op = TokenKind.TkShl) Or (op = TokenKind.TkShr) Then
        Printf(MSG_TA_FLOAT_BITWISE)
        ta_errCount = ta_errCount + 1
        Return unified
      End If
      If isCmp Then
        Return TaMakePrim(TypeKind.TyBoolean)
      End If
      Return unified
    End If
    If isCmp Then
      Return TaMakePrim(TypeKind.TyBoolean)
    End If
    Return unified
  End If

  If k = NodeKind.NdFieldAccess Then
    Dim baseId As Integer = g_nodes(id).left
    Dim fPos As Integer = g_nodes(id).intVal
    Dim fLen As Integer = g_nodes(id).lenVal
    If (baseId >= 0) And (g_nodes(baseId).kind = NodeKind.NdVar) Then
      Dim eIdx As Integer = TaFindEnum(g_nodes(baseId).intVal, g_nodes(baseId).lenVal)
      If eIdx >= 0 Then
        If TaHasEnumMem(eIdx, fPos, fLen) Then
          Return TaMakePrim(TypeKind.TyInt)
        End If
        Printf("Semantic Type Error: Enum '")
        TaPrintSlice(g_nodes(baseId).intVal, g_nodes(baseId).lenVal)
        Printf("' has no member variant: ")
        TaPrintSlice(fPos, fLen)
        Printf("\n")
        ta_errCount = ta_errCount + 1
        Return TaMakePrim(TypeKind.TyInt)
      End If
    End If
    Dim baseTy As TypeInfo = TaTypeOfExpr(baseId)
    If (baseTy.arrLen = 0) And (baseTy.kind = TypeKind.TyStruct) And (baseTy.typeIdx >= 0) Then
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
      Return TaMakePrim(TypeKind.TyInt)
    End If
    Printf("Semantic Type Error: Field access requires a structure value before .")
    TaPrintSlice(fPos, fLen)
    Printf("\n")
    ta_errCount = ta_errCount + 1
    Return TaMakePrim(TypeKind.TyInt)
  End If

  If k = NodeKind.NdIndexAccess Then
    Dim baseTy As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
    Return TaCheckIndex(baseTy, g_nodes(id).right)
  End If

  If k = NodeKind.NdArrayLit Then
    Dim cnt As Integer = g_nodes(id).childCount
    If cnt = 0 Then
      Dim emptyArr As TypeInfo With ( kind = TypeKind.TyInt, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 )
      Return emptyArr
    End If
    Dim firstTy As TypeInfo = TaTypeOfExpr(g_kids(g_nodes(id).firstChild))
    Dim i As Integer = 1
    While i < cnt Do
      TaTypeOfExpr(g_kids(g_nodes(id).firstChild + i))
      i = i + 1
    End While
    Dim arrTy As TypeInfo With ( kind = firstTy.kind, typeIdx = firstTy.typeIdx, arrLen = cnt, namePos = firstTy.namePos, nameLen = firstTy.nameLen )
    Return arrTy
  End If

  If k = NodeKind.NdStructInit Then
    Dim sPos As Integer = g_nodes(id).intVal
    Dim sLen As Integer = g_nodes(id).lenVal
    Dim sIdx As Integer = TaFindStruct(sPos, sLen)
    If sIdx < 0 Then
      Printf("Semantic Type Error: Unknown structure in With initializer: ")
      TaPrintSlice(sPos, sLen)
      Printf("\n")
      ta_errCount = ta_errCount + 1
      Return TaMakePrim(TypeKind.TyInt)
    End If
    Dim st As Integer = g_nodes(id).firstChild
    Dim cnt As Integer = g_nodes(id).childCount
    Dim i As Integer = 0
    While i < cnt Do
      Dim fi As Integer = g_kids(st + i)
      Dim fnPos As Integer = g_nodes(fi).intVal
      Dim fnLen As Integer = g_nodes(fi).lenVal
      ' Check duplicate field in With initializer
      Dim prev As Integer = 0
      While prev < i Do
        Dim pfi As Integer = g_kids(st + prev)
        If SlicesEqual(g_nodes(pfi).intVal, g_nodes(pfi).lenVal, fnPos, fnLen) Then
          Printf("Semantic Type Error: Duplicate field '")
          TaPrintSlice(fnPos, fnLen)
          Printf("' in '")
          TaPrintSlice(sPos, sLen)
          Printf("' With initializer\n")
          ta_errCount = ta_errCount + 1
        End If
        prev = prev + 1
      End While
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
      i = i + 1
    End While
    Dim res As TypeInfo With ( kind = TypeKind.TyStruct, typeIdx = sIdx, arrLen = 0, namePos = sPos, nameLen = sLen )
    Return res
  End If

  If k = NodeKind.NdSizeOf Then
    Dim ty As TypeInfo = TaResolveType(g_nodes(id).tyKind, 0, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    TaValidateType(ty)
    Return TaMakePrim(TypeKind.TyInt)
  End If

  If k = NodeKind.NdHeapAlloc Then
    Dim elemTy As TypeInfo = TaResolveType(g_nodes(id).tyKind, 0, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    TaValidateType(elemTy)
    If elemTy.kind = TypeKind.TyNothing Then
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

  If k = NodeKind.NdHeapRealloc Then
    Dim elemTy As TypeInfo = TaResolveType(g_nodes(id).tyKind, 0, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    TaValidateType(elemTy)
    Dim pt As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
    If (pt.arrLen = 0) And (pt.kind <> TypeKind.TyPointer) Then
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

  If k = NodeKind.NdArenaAlloc Then
    If g_nodes(id).op <> 1 Then
      Printf("Semantic Type Error: ArenaAlloc expects ByRef arena as argument 1\n")
      ta_errCount = ta_errCount + 1
    Else
      If Not TaIsLValue(g_nodes(id).left) Then
        Printf("Semantic Type Error: ByRef argument must be a variable, struct field, or array element\n")
        ta_errCount = ta_errCount + 1
      Else
        Dim adt As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
        If (adt.arrLen <> 0) Or (adt.kind <> TypeKind.TyArena) Then
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

  If k = NodeKind.NdCall Then
    Return TaTypeOfCall(id)
  End If

  Return TaMakePrim(TypeKind.TyInt)
End Function

Private Sub TaCheckStmt(id As Integer)
  Dim k As Integer = g_nodes(id).kind

  If k = NodeKind.NdDim Then
    Dim nPos As Integer = g_nodes(id).intVal
    Dim nLen As Integer = g_nodes(id).lenVal
    If TaFindLocal(nPos, nLen) >= 0 Then
      Printf("Semantic Type Error: Variable '")
      TaPrintSlice(nPos, nLen)
      Printf("' is already declared\n")
      ta_errCount = ta_errCount + 1
    End If
    Dim ty As TypeInfo = TaResolveType(g_nodes(id).tyKind, g_nodes(id).tyArrLen, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    Dim initId As Integer = g_nodes(id).left
    If (ty.arrLen = 0) And (ty.kind = TypeKind.TyPointer) And (initId >= 0) Then
      Dim ik As Integer = g_nodes(initId).kind
      If (ik = NodeKind.NdCall) Or (ik = NodeKind.NdStructInit) Then
        Dim sIdx As Integer = TaFindStruct(g_nodes(initId).intVal, g_nodes(initId).lenVal)
        If sIdx >= 0 Then
          ty = TypeInfo With ( kind = TypeKind.TyStruct, typeIdx = sIdx, arrLen = 0, namePos = g_nodes(initId).intVal, nameLen = g_nodes(initId).lenVal )
        End If
      End If
    End If
    TaValidateType(ty)
    If initId >= 0 Then
      If g_nodes(initId).kind = NodeKind.NdArrayLit Then
        If ty.arrLen > 0 Then
          Dim litCnt As Integer = g_nodes(initId).childCount
          If litCnt > ty.arrLen Then
            Printf("Semantic Type Error: Array initializer for '")
            TaPrintSlice(nPos, nLen)
            Printf("' has %d elements, exceeding size %d\n", litCnt, ty.arrLen)
            ta_errCount = ta_errCount + 1
          End If
          Dim elemTy As TypeInfo With ( kind = ty.kind, typeIdx = ty.typeIdx, arrLen = 0, namePos = ty.namePos, nameLen = ty.nameLen )
          Dim ei As Integer = 0
          While ei < litCnt Do
            Dim eNode As Integer = g_kids(g_nodes(initId).firstChild + ei)
            Dim eTy As TypeInfo = TaTypeOfExpr(eNode)
            If Not TaIsAssignable(elemTy, eTy, eNode) Then
              Printf("Semantic Type Error: Array element type mismatch for '")
              TaPrintSlice(nPos, nLen)
              Printf("'\n")
              ta_errCount = ta_errCount + 1
            End If
            ei = ei + 1
          End While
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
    ta_locals(ta_localCount) = TaLocalSym With ( namePos = nPos, nameLen = nLen, ty = ty )
    ta_localCount = ta_localCount + 1
    Return
  End If

  If k = NodeKind.NdStaticDim Then
    Dim nPos As Integer = g_nodes(id).intVal
    Dim nLen As Integer = g_nodes(id).lenVal
    If TaFindLocal(nPos, nLen) >= 0 Then
      Printf("Semantic Type Error: Variable '")
      TaPrintSlice(nPos, nLen)
      Printf("' is already declared\n")
      ta_errCount = ta_errCount + 1
    End If
    Dim ty As TypeInfo = TaResolveType(g_nodes(id).tyKind, g_nodes(id).tyArrLen, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    TaValidateType(ty)
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

  If k = NodeKind.NdAssign Then
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

  If k = NodeKind.NdFieldAssign Then
    Dim baseTy As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
    Dim fPos As Integer = g_nodes(id).intVal
    Dim fLen As Integer = g_nodes(id).lenVal
    Dim targetTy As TypeInfo = TaMakePrim(TypeKind.TyInt)
    If (baseTy.arrLen = 0) And (baseTy.kind = TypeKind.TyStruct) And (baseTy.typeIdx >= 0) Then
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

  If k = NodeKind.NdIndexAssign Then
    Dim baseTy As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
    Dim elemTy As TypeInfo = TaCheckIndex(baseTy, g_nodes(id).right)
    Dim valTy As TypeInfo = TaTypeOfExpr(g_nodes(id).third)
    If Not TaIsAssignable(elemTy, valTy, g_nodes(id).third) Then
      Printf("Semantic Type Error: Type assignment mismatch in array index assignment\n")
      ta_errCount = ta_errCount + 1
    End If
    Return
  End If

  If k = NodeKind.NdExprStmt Then
    TaTypeOfExpr(g_nodes(id).left)
    Return
  End If

  If k = NodeKind.NdReturn Then
    Dim exprId As Integer = g_nodes(id).left
    If exprId < 0 Then
      If ta_curRetTy.kind <> TypeKind.TyNothing Then
        Printf("Semantic Type Error: Function must return a value of type ")
        TaPrintType(ta_curRetTy)
        Printf("\n")
        ta_errCount = ta_errCount + 1
      End If
    Else
      Dim actualTy As TypeInfo = TaTypeOfExpr(exprId)
      If ta_curRetTy.kind = TypeKind.TyNothing Then
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

  If k = NodeKind.NdAssert Then
    Dim condTy As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
    If Not (((condTy.arrLen = 0) And (condTy.kind = TypeKind.TyBoolean)) Or TaIsIntType(condTy)) Then
      Printf("Semantic Type Error: Assert condition must be Boolean, got ")
      TaPrintType(condTy)
      Printf("\n")
      ta_errCount = ta_errCount + 1
    End If
    If g_nodes(id).right >= 0 Then
      Dim msgTy As TypeInfo = TaTypeOfExpr(g_nodes(id).right)
      If Not ((msgTy.arrLen = 0) And (msgTy.kind = TypeKind.TyPointer)) Then
        Printf("Semantic Type Error: Assert message must be a string Pointer, got ")
        TaPrintType(msgTy)
        Printf("\n")
        ta_errCount = ta_errCount + 1
      End If
    End If
    Return
  End If

  If k = NodeKind.NdIf Then
    Dim condTy As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
    If Not (((condTy.arrLen = 0) And (condTy.kind = TypeKind.TyBoolean)) Or TaIsIntType(condTy)) Then
      Printf("Semantic Type Error: If condition must be Boolean, got ")
      TaPrintType(condTy)
      Printf("\n")
      ta_errCount = ta_errCount + 1
    End If
    Dim savedLocals As Integer = ta_localCount
    Dim i As Integer = 0
    While i < g_nodes(id).childCount Do
      TaCheckStmt(g_kids(g_nodes(id).firstChild + i))
      i = i + 1
    End While
    ta_localCount = savedLocals
    i = 0
    While i < g_nodes(id).extraCount Do
      TaCheckStmt(g_kids(g_nodes(id).extraStart + i))
      i = i + 1
    End While
    ta_localCount = savedLocals
    Return
  End If

  If k = NodeKind.NdWhile Then
    Dim condTy As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
    If Not (((condTy.arrLen = 0) And (condTy.kind = TypeKind.TyBoolean)) Or TaIsIntType(condTy)) Then
      Printf("Semantic Type Error: While condition must be Boolean, got ")
      TaPrintType(condTy)
      Printf("\n")
      ta_errCount = ta_errCount + 1
    End If
    Dim savedLocals As Integer = ta_localCount
    Dim i As Integer = 0
    While i < g_nodes(id).childCount Do
      TaCheckStmt(g_kids(g_nodes(id).firstChild + i))
      i = i + 1
    End While
    ta_localCount = savedLocals
    Return
  End If

  If k = NodeKind.NdFor Then
    Dim stTy As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
    Dim enTy As TypeInfo = TaTypeOfExpr(g_nodes(id).right)
    If (Not TaIsIntType(stTy)) Or (Not TaIsIntType(enTy)) Then
      Printf("Semantic Type Error: For loop bounds must be integer types\n")
      ta_errCount = ta_errCount + 1
    End If
    Dim savedLocals As Integer = ta_localCount
    ta_locals(ta_localCount) = TaLocalSym With ( namePos = g_nodes(id).intVal, nameLen = g_nodes(id).lenVal, ty = TaMakePrim(TypeKind.TyInt) )
    ta_localCount = ta_localCount + 1
    Dim i As Integer = 0
    While i < g_nodes(id).childCount Do
      TaCheckStmt(g_kids(g_nodes(id).firstChild + i))
      i = i + 1
    End While
    ta_localCount = savedLocals
    Return
  End If

  If k = NodeKind.NdSelect Then
    Dim targetTy As TypeInfo = TaTypeOfExpr(g_nodes(id).left)
    Dim bi As Integer = 0
    While bi < g_nodes(id).childCount Do
      Dim br As Integer = g_kids(g_nodes(id).firstChild + bi)
      If g_nodes(br).op = 0 Then
        Dim vi As Integer = 0
        While vi < g_nodes(br).extraCount Do
          Dim ce As Integer = g_kids(g_nodes(br).extraStart + vi)
          Dim cTy As TypeInfo = TaTypeOfExpr(ce)
          If Not TaIsAssignable(targetTy, cTy, ce) Then
            Printf("Semantic Type Error: Select Case value type mismatch\n")
            ta_errCount = ta_errCount + 1
          End If
          vi = vi + 1
        End While
      End If
      Dim savedLocals As Integer = ta_localCount
      Dim si As Integer = 0
      While si < g_nodes(br).childCount Do
        TaCheckStmt(g_kids(g_nodes(br).firstChild + si))
        si = si + 1
      End While
      ta_localCount = savedLocals
      bi = bi + 1
    End While
    Return
  End If
End Sub

Public Function AnalyzeProgram(ByRef arena As Arena, progRoot As Integer) As Integer
  ta_enums = ArenaAlloc(ByRef arena, TaEnumInfo, 64)
  ta_enumMems = ArenaAlloc(ByRef arena, TaEnumMem, 512)
  ta_structs = ArenaAlloc(ByRef arena, TaStructInfo, 128)
  ta_fields = ArenaAlloc(ByRef arena, TaFieldInfo, 1024)
  ta_globals = ArenaAlloc(ByRef arena, TaGlobalInfo, 512)
  ta_funcs = ArenaAlloc(ByRef arena, TaFuncInfo, 512)
  ta_params = ArenaAlloc(ByRef arena, TaParamInfo, 2048)
  ta_locals = ArenaAlloc(ByRef arena, TaLocalSym, 512)

  ta_enumCount = 0
  ta_enumMemCount = 0
  ta_structCount = 0
  ta_fieldCount = 0
  ta_globalCount = 0
  ta_funcCount = 0
  ta_paramCount = 0
  ta_localCount = 0
  ta_errCount = 0

  Dim defStart As Integer = g_nodes(progRoot).firstChild
  Dim defCount As Integer = g_nodes(progRoot).childCount

  ' Pass 1a: Register Enums and Struct headers
  Dim i As Integer = 0
  While i < defCount Do
    Dim d As Integer = g_kids(defStart + i)
    Dim dk As Integer = g_nodes(d).kind
    If dk = NodeKind.NdEnumDef Then
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
    If dk = NodeKind.NdStructDef Then
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
    i = i + 1
  End While

  ' Pass 1b: Resolve Struct fields
  Dim si As Integer = 0
  i = 0
  While i < defCount Do
    Dim d As Integer = g_kids(defStart + i)
    If g_nodes(d).kind = NodeKind.NdStructDef Then
      ta_curModId = g_nodes(d).modId
      Dim fs As Integer = ta_fieldCount
      Dim fc As Integer = g_nodes(d).childCount
      Dim j As Integer = 0
      While j < fc Do
        Dim f As Integer = g_kids(g_nodes(d).firstChild + j)
        Dim fty As TypeInfo = TaResolveType(g_nodes(f).tyKind, g_nodes(f).tyArrLen, g_nodes(f).tyNamePos, g_nodes(f).tyNameLen)
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
    i = i + 1
  End While

  ' Pass 1c: Register Globals, Functions, and ExternDecls
  i = 0
  While i < defCount Do
    Dim d As Integer = g_kids(defStart + i)
    Dim dk As Integer = g_nodes(d).kind
    ta_curModId = g_nodes(d).modId
    If dk = NodeKind.NdGlobalVar Then
      Dim isPriv As Integer = 0
      If g_nodes(d).op = 2 Then
        isPriv = 1
      End If
      Dim gty As TypeInfo = TaResolveType(g_nodes(d).tyKind, g_nodes(d).tyArrLen, g_nodes(d).tyNamePos, g_nodes(d).tyNameLen)
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
    If (dk = NodeKind.NdFuncDef) Or (dk = NodeKind.NdExternDecl) Then
      Dim ps As Integer = ta_paramCount
      Dim pc As Integer = g_nodes(d).extraCount
      Dim j As Integer = 0
      While j < pc Do
        Dim pn As Integer = g_kids(g_nodes(d).extraStart + j)
        Dim pty As TypeInfo = TaResolveType(g_nodes(pn).tyKind, g_nodes(pn).tyArrLen, g_nodes(pn).tyNamePos, g_nodes(pn).tyNameLen)
        ta_params(ta_paramCount) = TaParamInfo With ( isByRef = g_nodes(pn).op, ty = pty )
        ta_paramCount = ta_paramCount + 1
        j = j + 1
      End While
      Dim isExt As Integer = 0
      Dim isVar As Integer = 0
      Dim isPriv As Integer = 0
      If dk = NodeKind.NdExternDecl Then
        isExt = 1
        isVar = g_nodes(d).op
      Else
        If g_nodes(d).op = 2 Then
          isPriv = 1
        End If
      End If
      ta_funcs(ta_funcCount) = TaFuncInfo With (
        namePos = g_nodes(d).intVal,
        nameLen = g_nodes(d).lenVal,
        isExtern = isExt,
        isVariadic = isVar,
        isPrivate = isPriv,
        modId = g_nodes(d).modId,
        retTy = TaResolveType(g_nodes(d).tyKind, 0, g_nodes(d).tyNamePos, g_nodes(d).tyNameLen),
        paramStart = ps,
        paramCount = pc
      )
      ta_funcCount = ta_funcCount + 1
    End If
    i = i + 1
  End While

  ' Pass 2: Validate Global initializers, ExternDecl types, and Function bodies
  Dim gi As Integer = 0
  Dim fi As Integer = 0
  i = 0
  While i < defCount Do
    Dim d As Integer = g_kids(defStart + i)
    Dim dk As Integer = g_nodes(d).kind
    ta_curModId = g_nodes(d).modId
    If dk = NodeKind.NdGlobalVar Then
      Dim gty As TypeInfo = ta_globals(gi).ty
      TaValidateType(gty)
      Dim initId As Integer = ta_globals(gi).initNode
      If initId >= 0 Then
        If (gty.arrLen > 0) And (g_nodes(initId).kind = NodeKind.NdArrayLit) Then
          If g_nodes(initId).childCount > gty.arrLen Then
            Printf("Semantic Type Error: Static array initializer has %d elements, exceeding size %d\n", g_nodes(initId).childCount, gty.arrLen)
            ta_errCount = ta_errCount + 1
          Else
            Dim elemTy As TypeInfo With ( kind = gty.kind, typeIdx = gty.typeIdx, arrLen = 0, namePos = gty.namePos, nameLen = gty.nameLen )
            Dim ei As Integer = 0
            While ei < g_nodes(initId).childCount Do
              Dim el As Integer = g_kids(g_nodes(initId).firstChild + ei)
              Dim elTy As TypeInfo = TaTypeOfExpr(el)
              If Not TaIsAssignable(elemTy, elTy, el) Then
                Printf("Semantic Type Error: Static array element type mismatch\n")
                ta_errCount = ta_errCount + 1
              End If
              ei = ei + 1
            End While
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
    If dk = NodeKind.NdExternDecl Then
      TaValidateType(ta_funcs(fi).retTy)
      Dim j As Integer = 0
      While j < ta_funcs(fi).paramCount Do
        TaValidateType(ta_params(ta_funcs(fi).paramStart + j).ty)
        j = j + 1
      End While
      fi = fi + 1
    End If
    If dk = NodeKind.NdFuncDef Then
      ta_curRetTy = ta_funcs(fi).retTy
      TaValidateType(ta_curRetTy)
      ta_localCount = 0
      Dim j As Integer = 0
      While j < g_nodes(d).extraCount Do
        Dim pn As Integer = g_kids(g_nodes(d).extraStart + j)
        Dim pty As TypeInfo = ta_params(ta_funcs(fi).paramStart + j).ty
        TaValidateType(pty)
        ta_locals(ta_localCount) = TaLocalSym With ( namePos = g_nodes(pn).intVal, nameLen = g_nodes(pn).lenVal, ty = pty )
        ta_localCount = ta_localCount + 1
        j = j + 1
      End While
      j = 0
      While j < g_nodes(d).childCount Do
        TaCheckStmt(g_kids(g_nodes(d).firstChild + j))
        j = j + 1
      End While
      fi = fi + 1
    End If
    If dk = NodeKind.NdTestDef Then
      ta_curRetTy = TaMakePrim(TypeKind.TyNothing)
      ta_localCount = 0
      Dim j As Integer = 0
      While j < g_nodes(d).childCount Do
        TaCheckStmt(g_kids(g_nodes(d).firstChild + j))
        j = j + 1
      End While
    End If
    i = i + 1
  End While

  Return ta_errCount
End Function
