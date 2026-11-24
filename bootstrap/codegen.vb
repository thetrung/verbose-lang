' VerBose Self-Hosting Compiler (vbc) - Complete Typed LLVM IR Code Generator

Include "parser.vb"

Declare Function fprintf(fp As Pointer, fmt As Pointer, ...) As Integer
Declare Function fputc(c As Integer, fp As Pointer) As Integer
Declare Function strtod(s As Pointer, endp As Pointer) As Double
Declare Function memcpy(ByRef dst As Long, ByRef src As Double, n As Long) As Pointer

Structure CgEnumInfo
  Dim namePos As Integer
  Dim nameLen As Integer
  Dim memStart As Integer
  Dim memCount As Integer
End Structure

Structure CgEnumMem
  Dim namePos As Integer
  Dim nameLen As Integer
  Dim value As Integer
End Structure

Structure CgStructInfo
  Dim namePos As Integer
  Dim nameLen As Integer
  Dim fieldStart As Integer
  Dim fieldCount As Integer
End Structure

Structure CgFieldInfo
  Dim namePos As Integer
  Dim nameLen As Integer
  Dim ty As TypeInfo
End Structure

Structure CgGlobalInfo
  Dim namePos As Integer
  Dim nameLen As Integer
  Dim isPublic As Integer
  Dim isPrivate As Integer
  Dim modId As Integer
  Dim ty As TypeInfo
  Dim initNode As Integer
End Structure

Structure CgParamInfo
  Dim isByRef As Integer
  Dim namePos As Integer
  Dim nameLen As Integer
  Dim ty As TypeInfo
End Structure

Structure CgFuncInfo
  Dim namePos As Integer
  Dim nameLen As Integer
  Dim isExtern As Integer
  Dim isVariadic As Integer
  Dim isPublic As Integer
  Dim isPrivate As Integer
  Dim modId As Integer
  Dim retTy As TypeInfo
  Dim paramStart As Integer
  Dim paramCount As Integer
End Structure

Structure CgLocalSym
  Dim namePos As Integer
  Dim nameLen As Integer
  Dim slotReg As Integer      ' >0 => %r<slotReg>, <0 => param %p<-(slotReg+1)>
  Dim isIndirect As Integer   ' 1 for local/global unsized slice slot holding ptr
  Dim staticPlusOne As Integer ' >0 => @__vb_static_<staticPlusOne - 1>
  Dim ty As TypeInfo
End Structure

Structure CgStaticLocal
  Dim ty As TypeInfo
  Dim initNode As Integer
End Structure

Structure CgStrConst
  Dim srcPos As Integer
  Dim srcLen As Integer
  Dim byteLen As Integer
End Structure

Private Static cg_out As Pointer
Private Static cg_reg As Integer = 1
Private Static cg_lbl As Integer = 1
Private Static cg_usesArena As Integer = 0
Private Static cg_curModId As Integer = 0
Private Static cg_curRetTy As TypeInfo

Private Static cg_enums() As CgEnumInfo
Private Static cg_enumCount As Integer = 0
Private Static cg_enumMems() As CgEnumMem
Private Static cg_enumMemCount As Integer = 0

Private Static cg_structs() As CgStructInfo
Private Static cg_structCount As Integer = 0
Private Static cg_fields() As CgFieldInfo
Private Static cg_fieldCount As Integer = 0

Private Static cg_globals() As CgGlobalInfo
Private Static cg_globalCount As Integer = 0

Private Static cg_funcs() As CgFuncInfo
Private Static cg_funcCount As Integer = 0
Private Static cg_params() As CgParamInfo
Private Static cg_paramCount As Integer = 0

Private Static cg_locals() As CgLocalSym
Private Static cg_localCount As Integer = 0

Private Static cg_strs() As CgStrConst
Private Static cg_strCount As Integer = 0

Private Static cg_staticLocals() As CgStaticLocal
Private Static cg_staticLocalCount As Integer = 0

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

Private Sub CgEmitSlice(pos As Integer, slen As Integer)
  Dim i As Integer = 0
  While i < slen Do
    Dim ch As Integer = g_srcPool(pos + i)
    fputc(ch, cg_out)
    i = i + 1
  End While
End Sub

Private Sub CgEmitSymName(pos As Integer, slen As Integer, modId As Integer, isPrivate As Integer)
  If isPrivate = 1 Then
    fprintf(cg_out, "__mod%d_", modId)
  End If
  CgEmitSlice(pos, slen)
End Sub

Private Function CgFindEnum(pos As Integer, slen As Integer) As Integer
  Dim i As Integer = 0
  While i < cg_enumCount Do
    If SlicesEqual(cg_enums(i).namePos, cg_enums(i).nameLen, pos, slen) Then
      Return i
    End If
    i = i + 1
  End While
  Return -1
End Function

Private Function CgFindEnumVal(enumIdx As Integer, memPos As Integer, memLen As Integer, ByRef outVal As Integer) As Boolean
  Dim st As Integer = cg_enums(enumIdx).memStart
  Dim cnt As Integer = cg_enums(enumIdx).memCount
  Dim i As Integer = 0
  While i < cnt Do
    If SlicesEqual(cg_enumMems(st + i).namePos, cg_enumMems(st + i).nameLen, memPos, memLen) Then
      outVal = cg_enumMems(st + i).value
      Return True
    End If
    i = i + 1
  End While
  Return False
End Function

Private Function CgFindStruct(pos As Integer, slen As Integer) As Integer
  Dim i As Integer = 0
  While i < cg_structCount Do
    If SlicesEqual(cg_structs(i).namePos, cg_structs(i).nameLen, pos, slen) Then
      Return i
    End If
    i = i + 1
  End While
  Return -1
End Function

Private Function CgFindStructField(structIdx As Integer, fPos As Integer, fLen As Integer, ByRef outTy As TypeInfo) As Integer
  Dim st As Integer = cg_structs(structIdx).fieldStart
  Dim cnt As Integer = cg_structs(structIdx).fieldCount
  Dim i As Integer = 0
  While i < cnt Do
    If SlicesEqual(cg_fields(st + i).namePos, cg_fields(st + i).nameLen, fPos, fLen) Then
      outTy = cg_fields(st + i).ty
      Return i
    End If
    i = i + 1
  End While
  Return -1
End Function

Private Function CgFindGlobal(pos As Integer, slen As Integer) As Integer
  Dim i As Integer = 0
  While i < cg_globalCount Do
    If (cg_globals(i).isPrivate = 1) And (cg_globals(i).modId = cg_curModId) And SlicesEqual(cg_globals(i).namePos, cg_globals(i).nameLen, pos, slen) Then
      Return i
    End If
    i = i + 1
  End While
  i = 0
  While i < cg_globalCount Do
    If (cg_globals(i).isPrivate = 0) And SlicesEqual(cg_globals(i).namePos, cg_globals(i).nameLen, pos, slen) Then
      Return i
    End If
    i = i + 1
  End While
  Return -1
End Function

Private Function CgFindFunc(pos As Integer, slen As Integer) As Integer
  Dim i As Integer = 0
  While i < cg_funcCount Do
    If (cg_funcs(i).isPrivate = 1) And (cg_funcs(i).modId = cg_curModId) And SlicesEqual(cg_funcs(i).namePos, cg_funcs(i).nameLen, pos, slen) Then
      Return i
    End If
    i = i + 1
  End While
  i = 0
  While i < cg_funcCount Do
    If (cg_funcs(i).isPrivate = 0) And SlicesEqual(cg_funcs(i).namePos, cg_funcs(i).nameLen, pos, slen) Then
      Return i
    End If
    i = i + 1
  End While
  Return -1
End Function

Private Function CgFindLocal(pos As Integer, slen As Integer) As Integer
  Dim i As Integer = cg_localCount - 1
  While i >= 0 Do
    If SlicesEqual(cg_locals(i).namePos, cg_locals(i).nameLen, pos, slen) Then
      Return i
    End If
    i = i - 1
  End While
  Return -1
End Function

Private Function CgResolveType(rawKind As Integer, arrLen As Integer, nPos As Integer, nLen As Integer) As TypeInfo
  Dim res As TypeInfo With {
    kind = rawKind,
    typeIdx = -1,
    arrLen = arrLen,
    namePos = nPos,
    nameLen = nLen
  }
  If rawKind = TypeKind.TyArena Then
    cg_usesArena = 1
  End If
  If rawKind = TypeKind.TyStruct Then
    Dim eIdx As Integer = CgFindEnum(nPos, nLen)
    If eIdx >= 0 Then
      res.kind = TypeKind.TyEnum
      res.typeIdx = eIdx
    Else
      Dim sIdx As Integer = CgFindStruct(nPos, nLen)
      res.typeIdx = sIdx
    End If
  End If
  Return res
End Function

Private Function CgIsAggregate(ty As TypeInfo) As Boolean
  If ty.arrLen > 0 Then
    Return True
  End If
  If ty.arrLen < 0 Then
    Return False
  End If
  If (ty.kind = TypeKind.TyArena) Or (ty.kind = TypeKind.TyStruct) Then
    Return True
  End If
  Return False
End Function

Private Function CgElemType(ty As TypeInfo) As TypeInfo
  Dim e As TypeInfo With {
    kind = ty.kind,
    typeIdx = ty.typeIdx,
    arrLen = 0,
    namePos = ty.namePos,
    nameLen = ty.nameLen
  }
  Return e
End Function

Private Function CgBaseAlign(kind As Integer, typeIdx As Integer) As Integer
  Select Case kind
    Case TypeKind.TyByte, TypeKind.TyBoolean, TypeKind.TyNothing
      Return 1
    Case TypeKind.TyShort
      Return 2
    Case TypeKind.TyInt, TypeKind.TyEnum, TypeKind.TySingle
      Return 4
    Case TypeKind.TyLong, TypeKind.TyPointer, TypeKind.TyArena, TypeKind.TyDouble
      Return 8
    Case TypeKind.TyStruct
      If typeIdx < 0 Then
        Return 8
      End If
      Dim st As Integer = cg_structs(typeIdx).fieldStart
      Dim cnt As Integer = cg_structs(typeIdx).fieldCount
      Dim mx As Integer = 1
      Dim i As Integer = 0
      While i < cnt Do
        Dim fa As Integer = CgTypeAlign(cg_fields(st + i).ty)
        If fa > mx Then
          mx = fa
        End If
        i = i + 1
      End While
      Return mx
    Case Else
      Return 8
  End Select
End Function

Private Function CgTypeAlign(ty As TypeInfo) As Integer
  If ty.arrLen < 0 Then
    Return 8
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
  Dim i As Integer = 0
  While i < cnt Do
    Dim fty As TypeInfo = cg_fields(st + i).ty
    Dim a As Integer = CgTypeAlign(fty)
    If a > mx Then
      mx = a
    End If
    off = CgAlignUp(off, a) + CgTypeSize(fty)
    i = i + 1
  End While
  Dim total As Integer = CgAlignUp(off, mx)
  If total = 0 Then
    Return 8
  End If
  Return total
End Function

Private Function CgTypeSize(ty As TypeInfo) As Integer
  If ty.arrLen < 0 Then
    Return 8
  End If
  Dim elemSz As Integer = 0
  Select Case ty.kind
    Case TypeKind.TyByte, TypeKind.TyBoolean
      elemSz = 1
    Case TypeKind.TyShort
      elemSz = 2
    Case TypeKind.TyInt, TypeKind.TyEnum, TypeKind.TySingle
      elemSz = 4
    Case TypeKind.TyLong, TypeKind.TyPointer, TypeKind.TyDouble
      elemSz = 8
    Case TypeKind.TyArena
      elemSz = 24
    Case TypeKind.TyStruct
      elemSz = CgStructSize(ty.typeIdx)
    Case Else
      elemSz = 0
  End Select
  If ty.arrLen > 0 Then
    Return ty.arrLen * elemSz
  End If
  Return elemSz
End Function

Private Sub CgEmitBaseLlvmType(kind As Integer, typeIdx As Integer, nPos As Integer, nLen As Integer)
  Select Case kind
    Case TypeKind.TyNothing
      fprintf(cg_out, "void")
    Case TypeKind.TyInt, TypeKind.TyEnum
      fprintf(cg_out, "i32")
    Case TypeKind.TyByte
      fprintf(cg_out, "i8")
    Case TypeKind.TyShort
      fprintf(cg_out, "i16")
    Case TypeKind.TyLong
      fprintf(cg_out, "i64")
    Case TypeKind.TySingle
      fprintf(cg_out, "float")
    Case TypeKind.TyDouble
      fprintf(cg_out, "double")
    Case TypeKind.TyBoolean
      fprintf(cg_out, "i1")
    Case TypeKind.TyPointer
      fprintf(cg_out, "ptr")
    Case TypeKind.TyArena
      cg_usesArena = 1
      fprintf(cg_out, "%%struct.__vb_arena")
    Case TypeKind.TyStruct
      fprintf(cg_out, "%%struct.")
      If typeIdx >= 0 Then
        CgEmitSlice(cg_structs(typeIdx).namePos, cg_structs(typeIdx).nameLen)
      Else
        CgEmitSlice(nPos, nLen)
      End If
    Case Else
      fprintf(cg_out, "i32")
  End Select
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
  If (p.isByRef = 1) Or (p.ty.arrLen <> 0) Or (p.ty.kind = TypeKind.TyStruct) Or (p.ty.kind = TypeKind.TyArena) Then
    fprintf(cg_out, "ptr")
  Else
    CgEmitLlvmType(p.ty)
  End If
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
  Select Case ty.kind
    Case TypeKind.TyInt, TypeKind.TyByte, TypeKind.TyShort, TypeKind.TyLong, TypeKind.TyEnum
      fprintf(cg_out, "0")
    Case TypeKind.TySingle, TypeKind.TyDouble
      fprintf(cg_out, "0.0")
    Case TypeKind.TyBoolean
      fprintf(cg_out, "false")
    Case TypeKind.TyPointer
      fprintf(cg_out, "null")
    Case TypeKind.TyArena, TypeKind.TyStruct
      fprintf(cg_out, "zeroinitializer")
    Case Else
      fprintf(cg_out, "0")
  End Select
End Sub

Private Function CgRegisterString(srcPos As Integer, srcLen As Integer) As Integer
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
  cg_strs(idx) = CgStrConst With {
    srcPos = srcPos,
    srcLen = srcLen,
    byteLen = bytes + 1
  }
  cg_strCount = cg_strCount + 1
  Return idx
End Function

Private Sub CgCollectStrings(id As Integer)
  If id < 0 Then
    Return
  End If
  Dim k As Integer = g_nodes(id).kind
  If k = NodeKind.NdStrLit Then
    Dim sid As Integer = CgRegisterString(g_nodes(id).intVal, g_nodes(id).lenVal)
    g_nodes(id).op = sid
    Return
  End If
  If k = NodeKind.NdStaticDim Then
    Dim sid As Integer = cg_staticLocalCount
    g_nodes(id).op = sid
    cg_staticLocals(sid) = CgStaticLocal With {
      ty = CgResolveType(g_nodes(id).tyKind, g_nodes(id).tyArrLen, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen),
      initNode = g_nodes(id).left
    }
    cg_staticLocalCount = cg_staticLocalCount + 1
  End If
  If k = NodeKind.NdArenaAlloc Then
    cg_usesArena = 1
  End If
  If k = NodeKind.NdCall Then
    If SliceEqualsStr(g_nodes(id).intVal, g_nodes(id).lenVal, "ArenaCreate", 11) Or SliceEqualsStr(g_nodes(id).intVal, g_nodes(id).lenVal, "ArenaFromBuffer", 15) Then
      cg_usesArena = 1
    End If
  End If
  CgCollectStrings(g_nodes(id).left)
  CgCollectStrings(g_nodes(id).right)
  CgCollectStrings(g_nodes(id).third)
  Dim i As Integer = 0
  While i < g_nodes(id).childCount Do
    CgCollectStrings(g_kids(g_nodes(id).firstChild + i))
    i = i + 1
  End While
  Dim j As Integer = 0
  While j < g_nodes(id).extraCount Do
    CgCollectStrings(g_kids(g_nodes(id).extraStart + j))
    j = j + 1
  End While
End Sub

Private Sub CgEmitStringGlobals()
  Dim i As Integer = 0
  While i < cg_strCount Do
    Dim sp As Integer = cg_strs(i).srcPos
    Dim sl As Integer = cg_strs(i).srcLen
    Dim bl As Integer = cg_strs(i).byteLen
    fprintf(cg_out, "@.str_%d = private unnamed_addr constant [%d x i8] c\"", i, bl)
    Dim j As Integer = 0
    While j < sl Do
      Dim ch As Integer = g_srcPool(sp + j)
      If (ch = 92) And ((j + 1) < sl) Then
        Dim nc As Integer = g_srcPool(sp + j + 1)
        If nc = 110 Then
          fprintf(cg_out, "\\0A")
        Else
          If nc = 114 Then
            fprintf(cg_out, "\\0D")
          Else
            If nc = 116 Then
              fprintf(cg_out, "\\09")
            Else
              If nc = 48 Then
                fprintf(cg_out, "\\00")
              Else
                fprintf(cg_out, "\\%02X", nc)
              End If
            End If
          End If
        End If
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
    i = i + 1
  End While
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
  If k = NodeKind.NdFloatLit Then
    If ty.kind = TypeKind.TySingle Then
      CgEmitSingleHexConst(g_nodes(exprId).intVal, g_nodes(exprId).lenVal, 0)
    Else
      CgEmitSlice(g_nodes(exprId).intVal, g_nodes(exprId).lenVal)
    End If
    Return
  End If
  If k = NodeKind.NdIntLit Then
    If (ty.kind = TypeKind.TyPointer) Or (ty.arrLen < 0) Then
      fprintf(cg_out, "null")
    Else
      If (ty.kind = TypeKind.TySingle) Or (ty.kind = TypeKind.TyDouble) Then
        fprintf(cg_out, "%d.0", g_nodes(exprId).intVal)
      Else
        fprintf(cg_out, "%d", g_nodes(exprId).intVal)
      End If
    End If
    Return
  End If
  If (k = NodeKind.NdNeg) And (g_nodes(exprId).left >= 0) Then
    Dim inner As Integer = g_nodes(exprId).left
    If g_nodes(inner).kind = NodeKind.NdFloatLit Then
      If ty.kind = TypeKind.TySingle Then
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
  If k = NodeKind.NdBoolLit Then
    If g_nodes(exprId).intVal <> 0 Then
      fprintf(cg_out, "true")
    Else
      fprintf(cg_out, "false")
    End If
    Return
  End If
  If k = NodeKind.NdStrLit Then
    fprintf(cg_out, "@.str_%d", g_nodes(exprId).op)
    Return
  End If
  If k = NodeKind.NdFieldAccess Then
    Dim baseId As Integer = g_nodes(exprId).left
    If g_nodes(baseId).kind = NodeKind.NdVar Then
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
  If (k = NodeKind.NdArrayLit) And (ty.arrLen > 0) Then
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
  If (k = NodeKind.NdStructInit) And (ty.kind = TypeKind.TyStruct) And (ty.typeIdx >= 0) Then
    Dim fst As Integer = cg_structs(ty.typeIdx).fieldStart
    Dim fcnt As Integer = cg_structs(ty.typeIdx).fieldCount
    Dim ist As Integer = g_nodes(exprId).firstChild
    Dim icnt As Integer = g_nodes(exprId).childCount
    fprintf(cg_out, "{ ")
    Dim i As Integer = 0
    While i < fcnt Do
      If i > 0 Then
        fprintf(cg_out, ", ")
      End If
      Dim fty As TypeInfo = cg_fields(fst + i).ty
      CgEmitLlvmType(fty)
      fprintf(cg_out, " ")
      Dim foundExpr As Integer = -1
      Dim j As Integer = 0
      While j < icnt Do
        Dim fi As Integer = g_kids(ist + j)
        If SlicesEqual(g_nodes(fi).intVal, g_nodes(fi).lenVal, cg_fields(fst + i).namePos, cg_fields(fst + i).nameLen) Then
          foundExpr = g_nodes(fi).left
        End If
        j = j + 1
      End While
      CgEmitConstInit(fty, foundExpr)
      i = i + 1
    End While
    fprintf(cg_out, " }")
    Return
  End If
  CgEmitZeroVal(ty)
End Sub

Private Function CgIntBits(kind As Integer) As Integer
  Select Case kind
    Case TypeKind.TyByte
      Return 8
    Case TypeKind.TyShort
      Return 16
    Case TypeKind.TyInt, TypeKind.TyEnum
      Return 32
    Case TypeKind.TyLong
      Return 64
    Case Else
      Return 0
  End Select
End Function

Private Function CgIsFloat(kind As Integer) As Boolean
  Return (kind = TypeKind.TySingle) Or (kind = TypeKind.TyDouble)
End Function

Private Function CgCoerceScalar(valReg As Integer, fromTy As TypeInfo, toTy As TypeInfo) As Integer
  If (toTy.kind = TypeKind.TyPointer) Or (toTy.arrLen < 0) Then
    If (fromTy.kind = TypeKind.TyPointer) Or (fromTy.arrLen <> 0) Or (fromTy.kind = TypeKind.TyStruct) Or (fromTy.kind = TypeKind.TyArena) Then
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

  If (fromTy.kind = TypeKind.TyDouble) And (toTy.kind = TypeKind.TySingle) Then
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = fptrunc double %%r%d to float\n", r, valReg)
    Return r
  End If
  If (fromTy.kind = TypeKind.TySingle) And (toTy.kind = TypeKind.TyDouble) Then
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
  Dim longTy As TypeInfo With { kind = TypeKind.TyLong, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
  Return CgCoerceScalar(valReg, fromTy, longTy)
End Function

Private Sub CgEmitStoreTyped(dstPtrReg As Integer, dstTy As TypeInfo, valReg As Integer, valTy As TypeInfo)
  If CgIsAggregate(dstTy) Then
    Dim sz As Integer = CgTypeSize(dstTy)
    fprintf(cg_out, "  call void @llvm.memcpy.p0.p0.i64(ptr align 8 %%r%d, ptr align 8 %%r%d, i64 %d, i1 false)\n", dstPtrReg, valReg, sz)
    Return
  End If
  Dim coerced As Integer = CgCoerceScalar(valReg, valTy, dstTy)
  Dim align As Integer = CgTypeAlign(dstTy)
  fprintf(cg_out, "  store ")
  CgEmitLlvmType(dstTy)
  fprintf(cg_out, " %%r%d, ptr %%r%d, align %d\n", coerced, dstPtrReg, align)
End Sub

Private Function CgIsVarIndexCall(id As Integer) As Boolean
  If (g_nodes(id).kind <> NodeKind.NdCall) Or (g_nodes(id).childCount <> 1) Then
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

' Returns %rN pointer register to the lvalue storage + fills outTy & outIsIndirect
Private Function CgEmitLValuePtr(id As Integer, ByRef outTy As TypeInfo, ByRef outIsIndirect As Integer) As Integer
  outIsIndirect = 0
  Dim k As Integer = g_nodes(id).kind

  If k = NodeKind.NdVar Then
    Dim pos As Integer = g_nodes(id).intVal
    Dim slen As Integer = g_nodes(id).lenVal
    Dim lIdx As Integer = CgFindLocal(pos, slen)
    If lIdx >= 0 Then
      outTy = cg_locals(lIdx).ty
      outIsIndirect = cg_locals(lIdx).isIndirect
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
      If outTy.arrLen < 0 Then
        outIsIndirect = 1
      End If
      Dim r As Integer = CgNextReg()
      fprintf(cg_out, "  %%r%d = getelementptr i8, ptr @", r)
      CgEmitSymName(pos, slen, cg_globals(gIdx).modId, cg_globals(gIdx).isPrivate)
      fprintf(cg_out, ", i64 0\n")
      Return r
    End If
    printf("vbc codegen error: undefined variable\n")
    Return 0
  End If

  If k = NodeKind.NdFieldAccess Then
    Dim baseTy As TypeInfo
    Dim baseInd As Integer = 0
    Dim basePtr As Integer = CgEmitLValuePtr(g_nodes(id).left, ByRef baseTy, ByRef baseInd)
    Dim fTy As TypeInfo
    Dim fIdx As Integer = CgFindStructField(baseTy.typeIdx, g_nodes(id).intVal, g_nodes(id).lenVal, ByRef fTy)
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = getelementptr inbounds ", r)
    CgEmitBaseLlvmType(baseTy.kind, baseTy.typeIdx, baseTy.namePos, baseTy.nameLen)
    fprintf(cg_out, ", ptr %%r%d, i32 0, i32 %d\n", basePtr, fIdx)
    outTy = fTy
    Return r
  End If

  If (k = NodeKind.NdIndexAccess) Or CgIsVarIndexCall(id) Then
    Dim baseNode As Integer = -1
    Dim idxNode As Integer = -1
    If k = NodeKind.NdIndexAccess Then
      baseNode = g_nodes(id).left
      idxNode = g_nodes(id).right
    Else
      Dim argNode As Integer = g_kids(g_nodes(id).firstChild)
      idxNode = g_nodes(argNode).left
      baseNode = NewNode(NodeKind.NdVar)
      g_nodes(baseNode).intVal = g_nodes(id).intVal
      g_nodes(baseNode).lenVal = g_nodes(id).lenVal
    End If

    Dim idxTy As TypeInfo
    Dim idxReg As Integer = CgEmitExpr(idxNode, ByRef idxTy)
    Dim idx64 As Integer = CgWidenToI64(idxReg, idxTy)

    Dim baseTy As TypeInfo
    Dim baseInd As Integer = 0
    Dim basePtr As Integer = CgEmitLValuePtr(baseNode, ByRef baseTy, ByRef baseInd)

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
        fprintf(cg_out, "  %%r%d = load ptr, ptr %%r%d, align 8\n", rawPtr, basePtr)
      End If
      Dim elemTy As TypeInfo = CgElemType(baseTy)
      Dim r As Integer = CgNextReg()
      fprintf(cg_out, "  %%r%d = getelementptr inbounds ", r)
      CgEmitLlvmType(elemTy)
      fprintf(cg_out, ", ptr %%r%d, i64 %%r%d\n", rawPtr, idx64)
      outTy = elemTy
      Return r
    End If

    If baseTy.kind = TypeKind.TyPointer Then
      Dim rawPtr As Integer = CgNextReg()
      fprintf(cg_out, "  %%r%d = load ptr, ptr %%r%d, align 8\n", rawPtr, basePtr)
      Dim r As Integer = CgNextReg()
      fprintf(cg_out, "  %%r%d = getelementptr inbounds i8, ptr %%r%d, i64 %%r%d\n", r, rawPtr, idx64)
      outTy = TypeInfo With { kind = TypeKind.TyByte, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
      Return r
    End If
  End If

  printf("vbc codegen error: invalid lvalue\n")
  Return 0
End Function

Private Function CgEmitByRefArg(exprId As Integer) As Integer
  Dim ty As TypeInfo
  Dim isInd As Integer = 0
  Dim ptrReg As Integer = CgEmitLValuePtr(exprId, ByRef ty, ByRef isInd)
  If (g_nodes(exprId).kind = NodeKind.NdVar) And (isInd = 1) Then
    Dim loaded As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = load ptr, ptr %%r%d, align 8\n", loaded, ptrReg)
    Return loaded
  End If
  Return ptrReg
End Function

Private Function CgEmitExpr(id As Integer, ByRef outTy As TypeInfo) As Integer
  Dim k As Integer = g_nodes(id).kind

  If k = NodeKind.NdIntLit Then
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = add i32 0, %d\n", r, g_nodes(id).intVal)
    outTy = TypeInfo With { kind = TypeKind.TyInt, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
    Return r
  End If

  If k = NodeKind.NdFloatLit Then
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = fadd double 0.0, ", r)
    CgEmitSlice(g_nodes(id).intVal, g_nodes(id).lenVal)
    fprintf(cg_out, "\n")
    outTy = TypeInfo With { kind = TypeKind.TyDouble, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
    Return r
  End If

  If k = NodeKind.NdBoolLit Then
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = add i1 0, %d\n", r, g_nodes(id).intVal)
    outTy = TypeInfo With { kind = TypeKind.TyBoolean, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
    Return r
  End If

  If k = NodeKind.NdStrLit Then
    Dim sid As Integer = g_nodes(id).op
    Dim bl As Integer = cg_strs(sid).byteLen
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = getelementptr inbounds [%d x i8], ptr @.str_%d, i64 0, i64 0\n", r, bl, sid)
    outTy = TypeInfo With { kind = TypeKind.TyPointer, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
    Return r
  End If

  If k = NodeKind.NdVar Then
    Dim vTy As TypeInfo
    Dim isInd As Integer = 0
    Dim ptrReg As Integer = CgEmitLValuePtr(id, ByRef vTy, ByRef isInd)
    outTy = vTy
    If CgIsAggregate(vTy) Then
      Return ptrReg
    End If
    If vTy.arrLen < 0 Then
      If isInd = 1 Then
        Dim r As Integer = CgNextReg()
        fprintf(cg_out, "  %%r%d = load ptr, ptr %%r%d, align 8\n", r, ptrReg)
        Return r
      End If
      Return ptrReg
    End If
    Dim r As Integer = CgNextReg()
    Dim align As Integer = CgTypeAlign(vTy)
    fprintf(cg_out, "  %%r%d = load ", r)
    CgEmitLlvmType(vTy)
    fprintf(cg_out, ", ptr %%r%d, align %d\n", ptrReg, align)
    Return r
  End If

  If k = NodeKind.NdFieldAccess Then
    ' Check EnumName.Variant first
    Dim baseId As Integer = g_nodes(id).left
    If g_nodes(baseId).kind = NodeKind.NdVar Then
      Dim eIdx As Integer = CgFindEnum(g_nodes(baseId).intVal, g_nodes(baseId).lenVal)
      If eIdx >= 0 Then
        Dim ev As Integer = 0
        If CgFindEnumVal(eIdx, g_nodes(id).intVal, g_nodes(id).lenVal, ByRef ev) Then
          Dim r As Integer = CgNextReg()
          fprintf(cg_out, "  %%r%d = add i32 0, %d\n", r, ev)
          outTy = TypeInfo With { kind = TypeKind.TyInt, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
          Return r
        End If
      End If
    End If
    Dim fTy As TypeInfo
    Dim isInd As Integer = 0
    Dim fPtr As Integer = CgEmitLValuePtr(id, ByRef fTy, ByRef isInd)
    outTy = fTy
    If CgIsAggregate(fTy) Then
      Return fPtr
    End If
    Dim r As Integer = CgNextReg()
    Dim align As Integer = CgTypeAlign(fTy)
    fprintf(cg_out, "  %%r%d = load ", r)
    CgEmitLlvmType(fTy)
    fprintf(cg_out, ", ptr %%r%d, align %d\n", fPtr, align)
    Return r
  End If

  If (k = NodeKind.NdIndexAccess) Or CgIsVarIndexCall(id) Then
    Dim eTy As TypeInfo
    Dim isInd As Integer = 0
    Dim ePtr As Integer = CgEmitLValuePtr(id, ByRef eTy, ByRef isInd)
    outTy = eTy
    If CgIsAggregate(eTy) Then
      Return ePtr
    End If
    Dim r As Integer = CgNextReg()
    Dim align As Integer = CgTypeAlign(eTy)
    fprintf(cg_out, "  %%r%d = load ", r)
    CgEmitLlvmType(eTy)
    fprintf(cg_out, ", ptr %%r%d, align %d\n", ePtr, align)
    Return r
  End If

  If k = NodeKind.NdNeg Then
    Dim t As TypeInfo
    Dim v As Integer = CgEmitExpr(g_nodes(id).left, ByRef t)
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

  If k = NodeKind.NdNot Then
    Dim t As TypeInfo
    Dim v As Integer = CgEmitExpr(g_nodes(id).left, ByRef t)
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = xor ", r)
    CgEmitLlvmType(t)
    fprintf(cg_out, " %%r%d, -1\n", v)
    outTy = t
    Return r
  End If

  If k = NodeKind.NdBinOp Then
    Dim t1 As TypeInfo
    Dim t2 As TypeInfo
    Dim v1 As Integer = CgEmitExpr(g_nodes(id).left, ByRef t1)
    Dim v2 As Integer = CgEmitExpr(g_nodes(id).right, ByRef t2)
    Dim unified As TypeInfo = t1
    If CgIsFloat(t1.kind) Or CgIsFloat(t2.kind) Then
      If (t1.kind = TypeKind.TySingle) And (t2.kind = TypeKind.TyDouble) And (g_nodes(g_nodes(id).right).kind = NodeKind.NdFloatLit) Then
        v2 = CgCoerceScalar(v2, t2, t1)
        unified = t1
      Else
        If (t2.kind = TypeKind.TySingle) And (t1.kind = TypeKind.TyDouble) And (g_nodes(g_nodes(id).left).kind = NodeKind.NdFloatLit) Then
          v1 = CgCoerceScalar(v1, t1, t2)
          unified = t2
        Else
          If t1.kind <> t2.kind Then
            If (t1.kind = TypeKind.TyDouble) Or (t2.kind = TypeKind.TyDouble) Then
              unified = TypeInfo With { kind = TypeKind.TyDouble, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
            Else
              unified = TypeInfo With { kind = TypeKind.TySingle, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
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
        If g_nodes(g_nodes(id).right).kind = NodeKind.NdIntLit Then
          v2 = CgCoerceScalar(v2, t2, t1)
          unified = t1
        Else
          If g_nodes(g_nodes(id).left).kind = NodeKind.NdIntLit Then
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
    Dim op As Integer = g_nodes(id).op
    outTy = unified
    If CgIsFloat(unified.kind) Then
      Select Case op
        Case TokenKind.TkPlus
          fprintf(cg_out, "  %%r%d = fadd ", r)
          CgEmitLlvmType(unified)
          fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
        Case TokenKind.TkMinus
          fprintf(cg_out, "  %%r%d = fsub ", r)
          CgEmitLlvmType(unified)
          fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
        Case TokenKind.TkStar
          fprintf(cg_out, "  %%r%d = fmul ", r)
          CgEmitLlvmType(unified)
          fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
        Case TokenKind.TkSlash
          fprintf(cg_out, "  %%r%d = fdiv ", r)
          CgEmitLlvmType(unified)
          fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
        Case TokenKind.TkMod
          fprintf(cg_out, "  %%r%d = frem ", r)
          CgEmitLlvmType(unified)
          fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
        Case TokenKind.TkEq
          fprintf(cg_out, "  %%r%d = fcmp oeq ", r)
          CgEmitLlvmType(unified)
          fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
          outTy = TypeInfo With { kind = TypeKind.TyBoolean, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
        Case TokenKind.TkNeq
          fprintf(cg_out, "  %%r%d = fcmp one ", r)
          CgEmitLlvmType(unified)
          fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
          outTy = TypeInfo With { kind = TypeKind.TyBoolean, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
        Case TokenKind.TkLt
          fprintf(cg_out, "  %%r%d = fcmp olt ", r)
          CgEmitLlvmType(unified)
          fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
          outTy = TypeInfo With { kind = TypeKind.TyBoolean, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
        Case TokenKind.TkGt
          fprintf(cg_out, "  %%r%d = fcmp ogt ", r)
          CgEmitLlvmType(unified)
          fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
          outTy = TypeInfo With { kind = TypeKind.TyBoolean, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
        Case TokenKind.TkLe
          fprintf(cg_out, "  %%r%d = fcmp ole ", r)
          CgEmitLlvmType(unified)
          fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
          outTy = TypeInfo With { kind = TypeKind.TyBoolean, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
        Case TokenKind.TkGe
          fprintf(cg_out, "  %%r%d = fcmp oge ", r)
          CgEmitLlvmType(unified)
          fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
          outTy = TypeInfo With { kind = TypeKind.TyBoolean, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
      End Select
      Return r
    End If
    Select Case op
      Case TokenKind.TkPlus
        fprintf(cg_out, "  %%r%d = add nsw ", r)
        CgEmitLlvmType(unified)
        fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
      Case TokenKind.TkMinus
        fprintf(cg_out, "  %%r%d = sub nsw ", r)
        CgEmitLlvmType(unified)
        fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
      Case TokenKind.TkStar
        fprintf(cg_out, "  %%r%d = mul nsw ", r)
        CgEmitLlvmType(unified)
        fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
      Case TokenKind.TkSlash
        fprintf(cg_out, "  %%r%d = sdiv ", r)
        CgEmitLlvmType(unified)
        fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
      Case TokenKind.TkMod
        fprintf(cg_out, "  %%r%d = srem ", r)
        CgEmitLlvmType(unified)
        fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
      Case TokenKind.TkAnd
        fprintf(cg_out, "  %%r%d = and ", r)
        CgEmitLlvmType(unified)
        fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
      Case TokenKind.TkOr
        fprintf(cg_out, "  %%r%d = or ", r)
        CgEmitLlvmType(unified)
        fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
      Case TokenKind.TkEq
        fprintf(cg_out, "  %%r%d = icmp eq ", r)
        CgEmitLlvmType(unified)
        fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
        outTy = TypeInfo With { kind = TypeKind.TyBoolean, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
      Case TokenKind.TkNeq
        fprintf(cg_out, "  %%r%d = icmp ne ", r)
        CgEmitLlvmType(unified)
        fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
        outTy = TypeInfo With { kind = TypeKind.TyBoolean, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
      Case TokenKind.TkLt
        fprintf(cg_out, "  %%r%d = icmp slt ", r)
        CgEmitLlvmType(unified)
        fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
        outTy = TypeInfo With { kind = TypeKind.TyBoolean, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
      Case TokenKind.TkGt
        fprintf(cg_out, "  %%r%d = icmp sgt ", r)
        CgEmitLlvmType(unified)
        fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
        outTy = TypeInfo With { kind = TypeKind.TyBoolean, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
      Case TokenKind.TkLe
        fprintf(cg_out, "  %%r%d = icmp sle ", r)
        CgEmitLlvmType(unified)
        fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
        outTy = TypeInfo With { kind = TypeKind.TyBoolean, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
      Case TokenKind.TkGe
        fprintf(cg_out, "  %%r%d = icmp sge ", r)
        CgEmitLlvmType(unified)
        fprintf(cg_out, " %%r%d, %%r%d\n", v1, v2)
        outTy = TypeInfo With { kind = TypeKind.TyBoolean, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
    End Select
    Return r
  End If

  If k = NodeKind.NdSizeOf Then
    Dim ty As TypeInfo = CgResolveType(g_nodes(id).tyKind, 0, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = add i32 0, %d\n", r, CgTypeSize(ty))
    outTy = TypeInfo With { kind = TypeKind.TyInt, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
    Return r
  End If

  If k = NodeKind.NdStructInit Then
    Dim sIdx As Integer = CgFindStruct(g_nodes(id).intVal, g_nodes(id).lenVal)
    Dim sTy As TypeInfo With {
      kind = TypeKind.TyStruct,
      typeIdx = sIdx,
      arrLen = 0,
      namePos = g_nodes(id).intVal,
      nameLen = g_nodes(id).lenVal
    }
    Dim slot As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = alloca ", slot)
    CgEmitLlvmType(sTy)
    fprintf(cg_out, ", align 8\n")
    fprintf(cg_out, "  store ")
    CgEmitLlvmType(sTy)
    fprintf(cg_out, " zeroinitializer, ptr %%r%d, align 8\n", slot)

    Dim st As Integer = g_nodes(id).firstChild
    Dim cnt As Integer = g_nodes(id).childCount
    Dim i As Integer = 0
    While i < cnt Do
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
      i = i + 1
    End While
    outTy = sTy
    Return slot
  End If

  If k = NodeKind.NdArenaAlloc Then
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
    outTy = TypeInfo With {
      kind = elemTy.kind,
      typeIdx = elemTy.typeIdx,
      arrLen = -1,
      namePos = elemTy.namePos,
      nameLen = elemTy.nameLen
    }
    Return resReg
  End If

  If k = NodeKind.NdHeapAlloc Then
    Dim elemTy As TypeInfo = CgResolveType(g_nodes(id).tyKind, 0, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    Dim elemSz As Integer = CgTypeSize(elemTy)
    Dim cntTy As TypeInfo
    Dim cntReg As Integer = CgEmitExpr(g_nodes(id).left, ByRef cntTy)
    Dim cnt64 As Integer = CgWidenToI64(cntReg, cntTy)
    Dim resReg As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = call ptr @calloc(i64 %%r%d, i64 %d)\n", resReg, cnt64, elemSz)
    outTy = TypeInfo With {
      kind = elemTy.kind,
      typeIdx = elemTy.typeIdx,
      arrLen = -1,
      namePos = elemTy.namePos,
      nameLen = elemTy.nameLen
    }
    Return resReg
  End If

  If k = NodeKind.NdHeapRealloc Then
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
    outTy = TypeInfo With {
      kind = elemTy.kind,
      typeIdx = elemTy.typeIdx,
      arrLen = -1,
      namePos = elemTy.namePos,
      nameLen = elemTy.nameLen
    }
    Return resReg
  End If

  If k = NodeKind.NdCall Then
    Return CgEmitCall(id, ByRef outTy)
  End If

  outTy = TypeInfo With { kind = TypeKind.TyInt, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
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
    outTy = TypeInfo With { kind = TypeKind.TyBoolean, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
    Return r
  End If

  ' Builtin: Len(arr)
  If SliceEqualsStr(nPos, nLen, "Len", 3) Then
    Dim a0 As Integer = g_nodes(g_kids(aStart)).left
    Dim aty As TypeInfo
    Dim ind As Integer = 0
    CgEmitLValuePtr(a0, ByRef aty, ByRef ind)
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = add i32 0, %d\n", r, aty.arrLen)
    outTy = TypeInfo With { kind = TypeKind.TyInt, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
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
    outTy = TypeInfo With { kind = TypeKind.TyArena, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
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
    outTy = TypeInfo With { kind = TypeKind.TyArena, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
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
    outTy = TypeInfo With { kind = TypeKind.TyPointer, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
    Return r
  End If

  ' Builtin: ArenaReset(ByRef a)
  If SliceEqualsStr(nPos, nLen, "ArenaReset", 10) Then
    cg_usesArena = 1
    Dim aPtr As Integer = CgEmitByRefArg(g_nodes(g_kids(aStart)).left)
    fprintf(cg_out, "  call void @__vb_arena_reset(ptr %%r%d)\n", aPtr)
    outTy = TypeInfo With { kind = TypeKind.TyNothing, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
    Return 0
  End If

  ' Builtin: ArenaDestroy(ByRef a)
  If SliceEqualsStr(nPos, nLen, "ArenaDestroy", 12) Then
    cg_usesArena = 1
    Dim aPtr As Integer = CgEmitByRefArg(g_nodes(g_kids(aStart)).left)
    fprintf(cg_out, "  call void @__vb_arena_destroy(ptr %%r%d)\n", aPtr)
    outTy = TypeInfo With { kind = TypeKind.TyNothing, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
    Return 0
  End If

  ' Builtins: ArenaUsed / ArenaMark / ArenaRemaining / ArenaCapacity
  If SliceEqualsStr(nPos, nLen, "ArenaUsed", 9) Or SliceEqualsStr(nPos, nLen, "ArenaMark", 9) Then
    cg_usesArena = 1
    Dim aPtr As Integer = CgEmitByRefArg(g_nodes(g_kids(aStart)).left)
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = call i64 @__vb_arena_used(ptr %%r%d)\n", r, aPtr)
    outTy = TypeInfo With { kind = TypeKind.TyLong, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
    Return r
  End If

  If SliceEqualsStr(nPos, nLen, "ArenaRemaining", 14) Then
    cg_usesArena = 1
    Dim aPtr As Integer = CgEmitByRefArg(g_nodes(g_kids(aStart)).left)
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = call i64 @__vb_arena_remaining(ptr %%r%d)\n", r, aPtr)
    outTy = TypeInfo With { kind = TypeKind.TyLong, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
    Return r
  End If

  If SliceEqualsStr(nPos, nLen, "ArenaCapacity", 13) Then
    cg_usesArena = 1
    Dim aPtr As Integer = CgEmitByRefArg(g_nodes(g_kids(aStart)).left)
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = call i64 @__vb_arena_capacity(ptr %%r%d)\n", r, aPtr)
    outTy = TypeInfo With { kind = TypeKind.TyLong, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
    Return r
  End If

  If SliceEqualsStr(nPos, nLen, "ArenaRewind", 11) Then
    cg_usesArena = 1
    Dim aPtr As Integer = CgEmitByRefArg(g_nodes(g_kids(aStart)).left)
    Dim mTy As TypeInfo
    Dim mReg As Integer = CgEmitExpr(g_nodes(g_kids(aStart + 1)).left, ByRef mTy)
    Dim m64 As Integer = CgWidenToI64(mReg, mTy)
    fprintf(cg_out, "  call void @__vb_arena_rewind(ptr %%r%d, i64 %%r%d)\n", aPtr, m64)
    outTy = TypeInfo With { kind = TypeKind.TyNothing, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
    Return 0
  End If

  ' Builtin: HeapAllocBytes(bytes)
  If SliceEqualsStr(nPos, nLen, "HeapAllocBytes", 14) Then
    Dim bTy As TypeInfo
    Dim bReg As Integer = CgEmitExpr(g_nodes(g_kids(aStart)).left, ByRef bTy)
    Dim b64 As Integer = CgWidenToI64(bReg, bTy)
    Dim r As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = call ptr @calloc(i64 1, i64 %%r%d)\n", r, b64)
    outTy = TypeInfo With { kind = TypeKind.TyPointer, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
    Return r
  End If

  ' Builtin: HeapFree(p)
  If SliceEqualsStr(nPos, nLen, "HeapFree", 8) Then
    Dim pTy As TypeInfo
    Dim pReg As Integer = CgEmitExpr(g_nodes(g_kids(aStart)).left, ByRef pTy)
    fprintf(cg_out, "  call void @free(ptr %%r%d)\n", pReg)
    outTy = TypeInfo With { kind = TypeKind.TyNothing, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
    Return 0
  End If

  ' Builtin: printf
  If SliceEqualsStr(nPos, nLen, "printf", 6) Then
    Dim argRegs(32) As Integer
    Dim argTys(32) As TypeInfo
    Dim i As Integer = 0
    While i < aCount Do
      Dim aNode As Integer = g_kids(aStart + i)
      Dim aTy As TypeInfo
      Dim aReg As Integer = CgEmitExpr(g_nodes(aNode).left, ByRef aTy)
      If aTy.kind = TypeKind.TySingle Then
        Dim dblTy As TypeInfo With { kind = TypeKind.TyDouble, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
        aReg = CgCoerceScalar(aReg, aTy, dblTy)
        aTy = dblTy
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
    outTy = TypeInfo With { kind = TypeKind.TyInt, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
    Return r
  End If

  ' Positional struct constructor: StructName(arg0, arg1, ...)
  Dim ctorStructIdx As Integer = CgFindStruct(nPos, nLen)
  If ctorStructIdx >= 0 Then
    Dim sTy As TypeInfo With {
      kind = TypeKind.TyStruct,
      typeIdx = ctorStructIdx,
      arrLen = 0,
      namePos = nPos,
      nameLen = nLen
    }
    Dim slot As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = alloca ", slot)
    CgEmitLlvmType(sTy)
    fprintf(cg_out, ", align 8\n")
    fprintf(cg_out, "  store ")
    CgEmitLlvmType(sTy)
    fprintf(cg_out, " zeroinitializer, ptr %%r%d, align 8\n", slot)
    Dim fst As Integer = cg_structs(ctorStructIdx).fieldStart
    Dim ci As Integer = 0
    While ci < aCount Do
      Dim fTy As TypeInfo = cg_fields(fst + ci).ty
      Dim aNode As Integer = g_kids(aStart + ci)
      Dim valTy As TypeInfo
      Dim valReg As Integer = CgEmitExpr(g_nodes(aNode).left, ByRef valTy)
      Dim fPtr As Integer = CgNextReg()
      fprintf(cg_out, "  %%r%d = getelementptr inbounds ", fPtr)
      CgEmitLlvmType(sTy)
      fprintf(cg_out, ", ptr %%r%d, i32 0, i32 %d\n", slot, ci)
      CgEmitStoreTyped(fPtr, fTy, valReg, valTy)
      ci = ci + 1
    End While
    outTy = sTy
    Return slot
  End If

  ' User Function / Sub / ExternDecl / C runtime builtin
  Dim fnIdx As Integer = CgFindFunc(nPos, nLen)
  Dim argRegs(32) As Integer
  Dim argTys(32) As TypeInfo
  Dim argIsPtr(32) As Integer

  Dim i As Integer = 0
  While i < aCount Do
    Dim aNode As Integer = g_kids(aStart + i)
    Dim isByRef As Integer = g_nodes(aNode).op
    Dim exprId As Integer = g_nodes(aNode).left
    If isByRef = 1 Then
      argRegs(i) = CgEmitByRefArg(exprId)
      argIsPtr(i) = 1
      argTys(i) = TypeInfo With { kind = TypeKind.TyPointer, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
    Else
      Dim aTy As TypeInfo
      Dim aReg As Integer = CgEmitExpr(exprId, ByRef aTy)
      If (fnIdx >= 0) And (i < cg_funcs(fnIdx).paramCount) Then
        Dim pInfo As CgParamInfo = cg_params(cg_funcs(fnIdx).paramStart + i)
        If (pInfo.ty.arrLen <> 0) Or (pInfo.ty.kind = TypeKind.TyStruct) Or (pInfo.ty.kind = TypeKind.TyArena) Then
          argRegs(i) = aReg
          argIsPtr(i) = 1
          argTys(i) = pInfo.ty
        Else
          argRegs(i) = CgCoerceScalar(aReg, aTy, pInfo.ty)
          argIsPtr(i) = 0
          argTys(i) = pInfo.ty
        End If
      Else
        If (fnIdx >= 0) And (cg_funcs(fnIdx).isVariadic = 1) And (aTy.kind = TypeKind.TySingle) Then
          Dim dblTy As TypeInfo With { kind = TypeKind.TyDouble, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
          aReg = CgCoerceScalar(aReg, aTy, dblTy)
          aTy = dblTy
        End If
        argRegs(i) = aReg
        If CgIsAggregate(aTy) Or (aTy.arrLen < 0) Then
          argIsPtr(i) = 1
        Else
          argIsPtr(i) = 0
        End If
        argTys(i) = aTy
      End If
    End If
    i = i + 1
  End While

  Dim retTy As TypeInfo With { kind = TypeKind.TyInt, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
  Dim isVar As Integer = 0
  If fnIdx >= 0 Then
    retTy = cg_funcs(fnIdx).retTy
    isVar = cg_funcs(fnIdx).isVariadic
  Else
    If SliceEqualsStr(nPos, nLen, "fopen", 5) Or SliceEqualsStr(nPos, nLen, "malloc", 6) Then
      retTy.kind = TypeKind.TyPointer
    Else
      If SliceEqualsStr(nPos, nLen, "free", 4) Then
        retTy.kind = TypeKind.TyNothing
      End If
    End If
  End If

  Dim resReg As Integer = 0
  If retTy.kind = TypeKind.TyNothing Then
    fprintf(cg_out, "  call void")
  Else
    resReg = CgNextReg()
    fprintf(cg_out, "  %%r%d = call ", resReg)
    CgEmitLlvmType(retTy)
  End If

  If (fnIdx >= 0) And (isVar = 1) Then
    fprintf(cg_out, " (")
    Dim pi As Integer = 0
    While pi < cg_funcs(fnIdx).paramCount Do
      If pi > 0 Then
        fprintf(cg_out, ", ")
      End If
      CgEmitParamLlvmType(cg_params(cg_funcs(fnIdx).paramStart + pi))
      pi = pi + 1
    End While
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
    CgEmitSlice(nPos, nLen)
  End If
  fprintf(cg_out, "(")
  Dim j As Integer = 0
  While j < aCount Do
    If j > 0 Then
      fprintf(cg_out, ", ")
    End If
    If argIsPtr(j) = 1 Then
      fprintf(cg_out, "ptr %%r%d", argRegs(j))
    Else
      CgEmitLlvmType(argTys(j))
      fprintf(cg_out, " %%r%d", argRegs(j))
    End If
    j = j + 1
  End While
  fprintf(cg_out, ")\n")

  outTy = retTy
  If CgIsAggregate(retTy) Then
    Dim tmp As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = alloca ", tmp)
    CgEmitLlvmType(retTy)
    fprintf(cg_out, ", align 8\n")
    fprintf(cg_out, "  store ")
    CgEmitLlvmType(retTy)
    fprintf(cg_out, " %%r%d, ptr %%r%d, align 8\n", resReg, tmp)
    Return tmp
  End If
  Return resReg
End Function

Private Function CgBlockTerminates(startIdx As Integer, cnt As Integer) As Boolean
  Dim i As Integer = 0
  While i < cnt Do
    Dim sid As Integer = g_kids(startIdx + i)
    If g_nodes(sid).kind = NodeKind.NdReturn Then
      Return True
    End If
    i = i + 1
  End While
  Return False
End Function

Private Sub CgEmitStmt(id As Integer)
  Dim k As Integer = g_nodes(id).kind

  If k = NodeKind.NdStaticDim Then
    Dim sid As Integer = g_nodes(id).op
    Dim ty As TypeInfo = cg_staticLocals(sid).ty
    Dim isInd As Integer = 0
    If ty.arrLen < 0 Then
      isInd = 1
    End If
    cg_locals(cg_localCount) = CgLocalSym With {
      namePos = g_nodes(id).intVal,
      nameLen = g_nodes(id).lenVal,
      slotReg = 0,
      isIndirect = isInd,
      staticPlusOne = sid + 1,
      ty = ty
    }
    cg_localCount = cg_localCount + 1
    Return
  End If

  If k = NodeKind.NdDim Then
    Dim ty As TypeInfo = CgResolveType(g_nodes(id).tyKind, g_nodes(id).tyArrLen, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    Dim slot As Integer = CgNextReg()
    Dim isInd As Integer = 0
    If ty.arrLen < 0 Then
      isInd = 1
    End If
    fprintf(cg_out, "  %%r%d = alloca ", slot)
    CgEmitLlvmType(ty)
    fprintf(cg_out, ", align 8\n")

    cg_locals(cg_localCount) = CgLocalSym With {
      namePos = g_nodes(id).intVal,
      nameLen = g_nodes(id).lenVal,
      slotReg = slot,
      isIndirect = isInd,
      ty = ty
    }
    cg_localCount = cg_localCount + 1

    Dim initId As Integer = g_nodes(id).left
    If initId < 0 Then
      Dim align As Integer = CgTypeAlign(ty)
      fprintf(cg_out, "  store ")
      CgEmitLlvmType(ty)
      fprintf(cg_out, " ")
      CgEmitZeroVal(ty)
      fprintf(cg_out, ", ptr %%r%d, align %d\n", slot, align)
      Return
    End If

    If (ty.arrLen > 0) And (g_nodes(initId).kind = NodeKind.NdArrayLit) Then
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
        CgEmitStoreTyped(ePtr, elemTy, vReg, vTy)
        i = i + 1
      End While
      Return
    End If

    Dim vTy As TypeInfo
    Dim vReg As Integer = CgEmitExpr(initId, ByRef vTy)
    CgEmitStoreTyped(slot, ty, vReg, vTy)
    Return
  End If

  If k = NodeKind.NdAssign Then
    Dim tmpVar As Integer = NewNode(NodeKind.NdVar)
    g_nodes(tmpVar).intVal = g_nodes(id).intVal
    g_nodes(tmpVar).lenVal = g_nodes(id).lenVal
    Dim dstTy As TypeInfo
    Dim isInd As Integer = 0
    Dim dstPtr As Integer = CgEmitLValuePtr(tmpVar, ByRef dstTy, ByRef isInd)
    Dim vTy As TypeInfo
    Dim vReg As Integer = CgEmitExpr(g_nodes(id).left, ByRef vTy)
    CgEmitStoreTyped(dstPtr, dstTy, vReg, vTy)
    Return
  End If

  If k = NodeKind.NdFieldAssign Then
    Dim tmpFa As Integer = NewNode(NodeKind.NdFieldAccess)
    g_nodes(tmpFa).left = g_nodes(id).left
    g_nodes(tmpFa).intVal = g_nodes(id).intVal
    g_nodes(tmpFa).lenVal = g_nodes(id).lenVal
    Dim vTy As TypeInfo
    Dim vReg As Integer = CgEmitExpr(g_nodes(id).right, ByRef vTy)
    Dim dstTy As TypeInfo
    Dim isInd As Integer = 0
    Dim dstPtr As Integer = CgEmitLValuePtr(tmpFa, ByRef dstTy, ByRef isInd)
    CgEmitStoreTyped(dstPtr, dstTy, vReg, vTy)
    Return
  End If

  If k = NodeKind.NdIndexAssign Then
    Dim tmpIa As Integer = NewNode(NodeKind.NdIndexAccess)
    g_nodes(tmpIa).left = g_nodes(id).left
    g_nodes(tmpIa).right = g_nodes(id).right
    Dim vTy As TypeInfo
    Dim vReg As Integer = CgEmitExpr(g_nodes(id).third, ByRef vTy)
    Dim dstTy As TypeInfo
    Dim isInd As Integer = 0
    Dim dstPtr As Integer = CgEmitLValuePtr(tmpIa, ByRef dstTy, ByRef isInd)
    CgEmitStoreTyped(dstPtr, dstTy, vReg, vTy)
    Return
  End If

  If k = NodeKind.NdExprStmt Then
    Dim dummyTy As TypeInfo
    CgEmitExpr(g_nodes(id).left, ByRef dummyTy)
    Return
  End If

  If k = NodeKind.NdReturn Then
    If g_nodes(id).left < 0 Then
      fprintf(cg_out, "  ret void\n")
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
    Return
  End If

  If k = NodeKind.NdIf Then
    Dim cTy As TypeInfo
    Dim cReg As Integer = CgEmitExpr(g_nodes(id).left, ByRef cTy)
    Dim cBit As Integer = cReg
    If cTy.kind <> TypeKind.TyBoolean Then
      cBit = CgNextReg()
      fprintf(cg_out, "  %%r%d = icmp ne ", cBit)
      CgEmitLlvmType(cTy)
      fprintf(cg_out, " %%r%d, 0\n", cReg)
    End If
    Dim lThen As Integer = CgNextLbl()
    Dim lElse As Integer = CgNextLbl()
    Dim lCont As Integer = CgNextLbl()
    Dim thenRet As Boolean = CgBlockTerminates(g_nodes(id).firstChild, g_nodes(id).childCount)
    Dim elseRet As Boolean = CgBlockTerminates(g_nodes(id).extraStart, g_nodes(id).extraCount)

    fprintf(cg_out, "  br i1 %%r%d, label %%L%d, label %%L%d\n", cBit, lThen, lElse)
    fprintf(cg_out, "L%d:\n", lThen)
    Dim saved1 As Integer = cg_localCount
    Dim i As Integer = 0
    While i < g_nodes(id).childCount Do
      CgEmitStmt(g_kids(g_nodes(id).firstChild + i))
      i = i + 1
    End While
    cg_localCount = saved1
    If Not thenRet Then
      fprintf(cg_out, "  br label %%L%d\n", lCont)
    End If

    fprintf(cg_out, "L%d:\n", lElse)
    Dim saved2 As Integer = cg_localCount
    Dim j As Integer = 0
    While j < g_nodes(id).extraCount Do
      CgEmitStmt(g_kids(g_nodes(id).extraStart + j))
      j = j + 1
    End While
    cg_localCount = saved2
    If Not elseRet Then
      fprintf(cg_out, "  br label %%L%d\n", lCont)
    End If

    If Not (thenRet And elseRet) Then
      fprintf(cg_out, "L%d:\n", lCont)
    End If
    Return
  End If

  If k = NodeKind.NdWhile Then
    Dim lCond As Integer = CgNextLbl()
    Dim lBody As Integer = CgNextLbl()
    Dim lEnd As Integer = CgNextLbl()
    fprintf(cg_out, "  br label %%L%d\n", lCond)
    fprintf(cg_out, "L%d:\n", lCond)
    Dim cTy As TypeInfo
    Dim cReg As Integer = CgEmitExpr(g_nodes(id).left, ByRef cTy)
    Dim cBit As Integer = cReg
    If cTy.kind <> TypeKind.TyBoolean Then
      cBit = CgNextReg()
      fprintf(cg_out, "  %%r%d = icmp ne ", cBit)
      CgEmitLlvmType(cTy)
      fprintf(cg_out, " %%r%d, 0\n", cReg)
    End If
    fprintf(cg_out, "  br i1 %%r%d, label %%L%d, label %%L%d\n", cBit, lBody, lEnd)
    fprintf(cg_out, "L%d:\n", lBody)
    Dim saved As Integer = cg_localCount
    Dim i As Integer = 0
    While i < g_nodes(id).childCount Do
      CgEmitStmt(g_kids(g_nodes(id).firstChild + i))
      i = i + 1
    End While
    cg_localCount = saved
    fprintf(cg_out, "  br label %%L%d\n", lCond)
    fprintf(cg_out, "L%d:\n", lEnd)
    Return
  End If

  If k = NodeKind.NdFor Then
    Dim lCond As Integer = CgNextLbl()
    Dim lBody As Integer = CgNextLbl()
    Dim lInc As Integer = CgNextLbl()
    Dim lEnd As Integer = CgNextLbl()
    Dim sTy As TypeInfo
    Dim sReg As Integer = CgEmitExpr(g_nodes(id).left, ByRef sTy)
    Dim intTy As TypeInfo With { kind = TypeKind.TyInt, typeIdx = -1, arrLen = 0, namePos = 0, nameLen = 0 }
    Dim s32 As Integer = CgCoerceScalar(sReg, sTy, intTy)

    Dim saved As Integer = cg_localCount
    Dim slot As Integer = -1
    Dim existing As Integer = CgFindLocal(g_nodes(id).intVal, g_nodes(id).lenVal)
    If existing >= 0 Then
      slot = cg_locals(existing).slotReg
    Else
      slot = CgNextReg()
      fprintf(cg_out, "  %%r%d = alloca i32, align 4\n", slot)
      cg_locals(cg_localCount) = CgLocalSym With {
        namePos = g_nodes(id).intVal,
        nameLen = g_nodes(id).lenVal,
        slotReg = slot,
        isIndirect = 0,
        ty = intTy
      }
      cg_localCount = cg_localCount + 1
    End If
    fprintf(cg_out, "  store i32 %%r%d, ptr %%r%d, align 4\n", s32, slot)
    fprintf(cg_out, "  br label %%L%d\n", lCond)

    fprintf(cg_out, "L%d:\n", lCond)
    Dim curV As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = load i32, ptr %%r%d, align 4\n", curV, slot)
    Dim fTy As TypeInfo
    Dim fReg As Integer = CgEmitExpr(g_nodes(id).right, ByRef fTy)
    Dim f32 As Integer = CgCoerceScalar(fReg, fTy, intTy)
    Dim cmp As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = icmp sle i32 %%r%d, %%r%d\n", cmp, curV, f32)
    fprintf(cg_out, "  br i1 %%r%d, label %%L%d, label %%L%d\n", cmp, lBody, lEnd)

    fprintf(cg_out, "L%d:\n", lBody)
    Dim savedBody As Integer = cg_localCount
    Dim i As Integer = 0
    While i < g_nodes(id).childCount Do
      CgEmitStmt(g_kids(g_nodes(id).firstChild + i))
      i = i + 1
    End While
    cg_localCount = savedBody
    fprintf(cg_out, "  br label %%L%d\n", lInc)

    fprintf(cg_out, "L%d:\n", lInc)
    Dim relV As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = load i32, ptr %%r%d, align 4\n", relV, slot)
    Dim incV As Integer = CgNextReg()
    fprintf(cg_out, "  %%r%d = add nsw i32 %%r%d, 1\n", incV, relV)
    fprintf(cg_out, "  store i32 %%r%d, ptr %%r%d, align 4\n", incV, slot)
    fprintf(cg_out, "  br label %%L%d\n", lCond)

    fprintf(cg_out, "L%d:\n", lEnd)
    cg_localCount = saved
    Return
  End If

  If k = NodeKind.NdSelect Then
    Dim tTy As TypeInfo
    Dim tReg As Integer = CgEmitExpr(g_nodes(id).left, ByRef tTy)
    Dim lExit As Integer = CgNextLbl()
    Dim hasDef As Integer = g_nodes(id).op
    Dim lDef As Integer = lExit
    Dim anyFalls As Boolean = False
    If hasDef = 1 Then
      lDef = CgNextLbl()
    Else
      anyFalls = True
    End If

    Dim brStart As Integer = g_nodes(id).firstChild
    Dim brCount As Integer = g_nodes(id).childCount
    Dim bi As Integer = 0
    While bi < brCount Do
      Dim br As Integer = g_kids(brStart + bi)
      Dim lMatch As Integer = CgNextLbl()
      Dim lNext As Integer = CgNextLbl()
      Dim vStart As Integer = g_nodes(br).extraStart
      Dim vCount As Integer = g_nodes(br).extraCount
      Dim accCond As Integer = -1
      Dim vi As Integer = 0
      While vi < vCount Do
        Dim cTy As TypeInfo
        Dim cReg As Integer = CgEmitExpr(g_kids(vStart + vi), ByRef cTy)
        Dim coerced As Integer = CgCoerceScalar(cReg, cTy, tTy)
        Dim eqR As Integer = CgNextReg()
        fprintf(cg_out, "  %%r%d = icmp eq ", eqR)
        CgEmitLlvmType(tTy)
        fprintf(cg_out, " %%r%d, %%r%d\n", tReg, coerced)
        If accCond < 0 Then
          accCond = eqR
        Else
          Dim orR As Integer = CgNextReg()
          fprintf(cg_out, "  %%r%d = or i1 %%r%d, %%r%d\n", orR, accCond, eqR)
          accCond = orR
        End If
        vi = vi + 1
      End While
      fprintf(cg_out, "  br i1 %%r%d, label %%L%d, label %%L%d\n", accCond, lMatch, lNext)
      fprintf(cg_out, "L%d:\n", lMatch)
      Dim savedB As Integer = cg_localCount
      Dim si As Integer = 0
      While si < g_nodes(br).childCount Do
        CgEmitStmt(g_kids(g_nodes(br).firstChild + si))
        si = si + 1
      End While
      cg_localCount = savedB
      If Not CgBlockTerminates(g_nodes(br).firstChild, g_nodes(br).childCount) Then
        anyFalls = True
        fprintf(cg_out, "  br label %%L%d\n", lExit)
      End If
      fprintf(cg_out, "L%d:\n", lNext)
      bi = bi + 1
    End While

    fprintf(cg_out, "  br label %%L%d\n", lDef)
    If hasDef = 1 Then
      fprintf(cg_out, "L%d:\n", lDef)
      Dim savedD As Integer = cg_localCount
      Dim di As Integer = 0
      While di < g_nodes(id).extraCount Do
        CgEmitStmt(g_kids(g_nodes(id).extraStart + di))
        di = di + 1
      End While
      cg_localCount = savedD
      If Not CgBlockTerminates(g_nodes(id).extraStart, g_nodes(id).extraCount) Then
        anyFalls = True
        fprintf(cg_out, "  br label %%L%d\n", lExit)
      End If
    End If
    If anyFalls Then
      fprintf(cg_out, "L%d:\n", lExit)
    End If
    Return
  End If
End Sub

Private Sub CgEmitFunc(fnNodeId As Integer, fnIdx As Integer)
  cg_reg = 1
  cg_localCount = 0
  Dim fi As CgFuncInfo = cg_funcs(fnIdx)
  cg_curRetTy = fi.retTy
  cg_curModId = fi.modId

  If fi.isPublic = 1 Then
    fprintf(cg_out, "define ")
  Else
    fprintf(cg_out, "define internal ")
  End If
  CgEmitLlvmType(fi.retTy)
  fprintf(cg_out, " @")
  CgEmitSymName(fi.namePos, fi.nameLen, fi.modId, fi.isPrivate)
  fprintf(cg_out, "(")
  Dim i As Integer = 0
  While i < fi.paramCount Do
    If i > 0 Then
      fprintf(cg_out, ", ")
    End If
    CgEmitParamLlvmType(cg_params(fi.paramStart + i))
    fprintf(cg_out, " %%p%d", i)
    i = i + 1
  End While
  fprintf(cg_out, ") {\nentry:\n")

  ' Bind parameters to local slots
  Dim j As Integer = 0
  While j < fi.paramCount Do
    Dim p As CgParamInfo = cg_params(fi.paramStart + j)
    Dim directPtr As Boolean = (p.isByRef = 1) Or (p.ty.arrLen < 0)
    If directPtr Then
      cg_locals(cg_localCount) = CgLocalSym With {
        namePos = p.namePos,
        nameLen = p.nameLen,
        slotReg = -(j + 1),
        isIndirect = 0,
        ty = p.ty
      }
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
      cg_locals(cg_localCount) = CgLocalSym With {
        namePos = p.namePos,
        nameLen = p.nameLen,
        slotReg = slot,
        isIndirect = 0,
        ty = p.ty
      }
      cg_localCount = cg_localCount + 1
    End If
    j = j + 1
  End While

  Dim bStart As Integer = g_nodes(fnNodeId).firstChild
  Dim bCount As Integer = g_nodes(fnNodeId).childCount
  Dim k As Integer = 0
  While k < bCount Do
    CgEmitStmt(g_kids(bStart + k))
    k = k + 1
  End While

  If fi.retTy.kind = TypeKind.TyNothing Then
    If Not CgBlockTerminates(bStart, bCount) Then
      fprintf(cg_out, "  ret void\n")
    End If
  End If
  fprintf(cg_out, "}\n\n")
End Sub

Private Sub CgEmitArenaRuntime()
  fprintf(cg_out, "declare void @llvm.memset.p0.i64(ptr nocapture writeonly, i8, i64, i1 immarg)\n\n")
  fprintf(cg_out, "define internal void @__vb_arena_init_heap(ptr %%a, i64 %%cap) {\n")
  fprintf(cg_out, "entry:\n")
  fprintf(cg_out, "  %%valid = icmp sgt i64 %%cap, 0\n")
  fprintf(cg_out, "  br i1 %%valid, label %%alloc_blk, label %%zero_blk\n")
  fprintf(cg_out, "alloc_blk:\n")
  fprintf(cg_out, "  %%mem = call ptr @calloc(i64 1, i64 %%cap)\n")
  fprintf(cg_out, "  %%ok = icmp ne ptr %%mem, null\n")
  fprintf(cg_out, "  br i1 %%ok, label %%store_ok, label %%zero_blk\n")
  fprintf(cg_out, "store_ok:\n")
  fprintf(cg_out, "  %%p0 = getelementptr inbounds %%struct.__vb_arena, ptr %%a, i32 0, i32 0\n")
  fprintf(cg_out, "  store ptr %%mem, ptr %%p0, align 8\n")
  fprintf(cg_out, "  %%p1 = getelementptr inbounds %%struct.__vb_arena, ptr %%a, i32 0, i32 1\n")
  fprintf(cg_out, "  store i64 %%cap, ptr %%p1, align 8\n")
  fprintf(cg_out, "  %%p2 = getelementptr inbounds %%struct.__vb_arena, ptr %%a, i32 0, i32 2\n")
  fprintf(cg_out, "  store i64 0, ptr %%p2, align 8\n")
  fprintf(cg_out, "  ret void\n")
  fprintf(cg_out, "zero_blk:\n")
  fprintf(cg_out, "  store %%struct.__vb_arena zeroinitializer, ptr %%a, align 8\n")
  fprintf(cg_out, "  ret void\n")
  fprintf(cg_out, "}\n\n")

  fprintf(cg_out, "define internal void @__vb_arena_init_buf(ptr %%a, ptr %%buf, i64 %%cap) {\n")
  fprintf(cg_out, "entry:\n")
  fprintf(cg_out, "  %%valid = icmp sgt i64 %%cap, 0\n")
  fprintf(cg_out, "  br i1 %%valid, label %%store_ok, label %%zero_blk\n")
  fprintf(cg_out, "store_ok:\n")
  fprintf(cg_out, "  %%p0 = getelementptr inbounds %%struct.__vb_arena, ptr %%a, i32 0, i32 0\n")
  fprintf(cg_out, "  store ptr %%buf, ptr %%p0, align 8\n")
  fprintf(cg_out, "  %%p1 = getelementptr inbounds %%struct.__vb_arena, ptr %%a, i32 0, i32 1\n")
  fprintf(cg_out, "  store i64 %%cap, ptr %%p1, align 8\n")
  fprintf(cg_out, "  %%p2 = getelementptr inbounds %%struct.__vb_arena, ptr %%a, i32 0, i32 2\n")
  fprintf(cg_out, "  store i64 0, ptr %%p2, align 8\n")
  fprintf(cg_out, "  ret void\n")
  fprintf(cg_out, "zero_blk:\n")
  fprintf(cg_out, "  store %%struct.__vb_arena zeroinitializer, ptr %%a, align 8\n")
  fprintf(cg_out, "  ret void\n")
  fprintf(cg_out, "}\n\n")

  fprintf(cg_out, "define internal ptr @__vb_arena_alloc(ptr %%a, i64 %%size) {\n")
  fprintf(cg_out, "entry:\n")
  fprintf(cg_out, "  %%le0 = icmp sle i64 %%size, 0\n")
  fprintf(cg_out, "  br i1 %%le0, label %%oom, label %%align_blk\n")
  fprintf(cg_out, "align_blk:\n")
  fprintf(cg_out, "  %%plus7 = add i64 %%size, 7\n")
  fprintf(cg_out, "  %%aligned = and i64 %%plus7, -8\n")
  fprintf(cg_out, "  %%cap_ptr = getelementptr inbounds %%struct.__vb_arena, ptr %%a, i32 0, i32 1\n")
  fprintf(cg_out, "  %%cap = load i64, ptr %%cap_ptr, align 8\n")
  fprintf(cg_out, "  %%off_ptr = getelementptr inbounds %%struct.__vb_arena, ptr %%a, i32 0, i32 2\n")
  fprintf(cg_out, "  %%off = load i64, ptr %%off_ptr, align 8\n")
  fprintf(cg_out, "  %%new_off = add i64 %%off, %%aligned\n")
  fprintf(cg_out, "  %%wrap = icmp ult i64 %%new_off, %%off\n")
  fprintf(cg_out, "  %%ovf = icmp ugt i64 %%new_off, %%cap\n")
  fprintf(cg_out, "  %%fail = or i1 %%wrap, %%ovf\n")
  fprintf(cg_out, "  br i1 %%fail, label %%oom, label %%ok\n")
  fprintf(cg_out, "ok:\n")
  fprintf(cg_out, "  %%base_slot = getelementptr inbounds %%struct.__vb_arena, ptr %%a, i32 0, i32 0\n")
  fprintf(cg_out, "  %%base = load ptr, ptr %%base_slot, align 8\n")
  fprintf(cg_out, "  %%isnull = icmp eq ptr %%base, null\n")
  fprintf(cg_out, "  br i1 %%isnull, label %%oom, label %%commit\n")
  fprintf(cg_out, "commit:\n")
  fprintf(cg_out, "  store i64 %%new_off, ptr %%off_ptr, align 8\n")
  fprintf(cg_out, "  %%res = getelementptr inbounds i8, ptr %%base, i64 %%off\n")
  fprintf(cg_out, "  call void @llvm.memset.p0.i64(ptr align 8 %%res, i8 0, i64 %%aligned, i1 false)\n")
  fprintf(cg_out, "  ret ptr %%res\n")
  fprintf(cg_out, "oom:\n")
  fprintf(cg_out, "  ret ptr null\n")
  fprintf(cg_out, "}\n\n")

  fprintf(cg_out, "define internal void @__vb_arena_reset(ptr %%a) {\n")
  fprintf(cg_out, "  %%off_ptr = getelementptr inbounds %%struct.__vb_arena, ptr %%a, i32 0, i32 2\n")
  fprintf(cg_out, "  store i64 0, ptr %%off_ptr, align 8\n")
  fprintf(cg_out, "  ret void\n")
  fprintf(cg_out, "}\n\n")

  fprintf(cg_out, "define internal i64 @__vb_arena_used(ptr %%a) {\n")
  fprintf(cg_out, "  %%off_ptr = getelementptr inbounds %%struct.__vb_arena, ptr %%a, i32 0, i32 2\n")
  fprintf(cg_out, "  %%off = load i64, ptr %%off_ptr, align 8\n")
  fprintf(cg_out, "  ret i64 %%off\n")
  fprintf(cg_out, "}\n\n")

  fprintf(cg_out, "define internal i64 @__vb_arena_capacity(ptr %%a) {\n")
  fprintf(cg_out, "  %%cap_ptr = getelementptr inbounds %%struct.__vb_arena, ptr %%a, i32 0, i32 1\n")
  fprintf(cg_out, "  %%cap = load i64, ptr %%cap_ptr, align 8\n")
  fprintf(cg_out, "  ret i64 %%cap\n")
  fprintf(cg_out, "}\n\n")

  fprintf(cg_out, "define internal i64 @__vb_arena_remaining(ptr %%a) {\n")
  fprintf(cg_out, "  %%cap_ptr = getelementptr inbounds %%struct.__vb_arena, ptr %%a, i32 0, i32 1\n")
  fprintf(cg_out, "  %%cap = load i64, ptr %%cap_ptr, align 8\n")
  fprintf(cg_out, "  %%off_ptr = getelementptr inbounds %%struct.__vb_arena, ptr %%a, i32 0, i32 2\n")
  fprintf(cg_out, "  %%off = load i64, ptr %%off_ptr, align 8\n")
  fprintf(cg_out, "  %%rem = sub i64 %%cap, %%off\n")
  fprintf(cg_out, "  ret i64 %%rem\n")
  fprintf(cg_out, "}\n\n")

  fprintf(cg_out, "define internal void @__vb_arena_rewind(ptr %%a, i64 %%mark) {\n")
  fprintf(cg_out, "entry:\n")
  fprintf(cg_out, "  %%off_ptr = getelementptr inbounds %%struct.__vb_arena, ptr %%a, i32 0, i32 2\n")
  fprintf(cg_out, "  %%off = load i64, ptr %%off_ptr, align 8\n")
  fprintf(cg_out, "  %%nonneg = icmp sge i64 %%mark, 0\n")
  fprintf(cg_out, "  %%le_off = icmp sle i64 %%mark, %%off\n")
  fprintf(cg_out, "  %%ok = and i1 %%nonneg, %%le_off\n")
  fprintf(cg_out, "  br i1 %%ok, label %%do_rewind, label %%done\n")
  fprintf(cg_out, "do_rewind:\n")
  fprintf(cg_out, "  store i64 %%mark, ptr %%off_ptr, align 8\n")
  fprintf(cg_out, "  br label %%done\n")
  fprintf(cg_out, "done:\n")
  fprintf(cg_out, "  ret void\n")
  fprintf(cg_out, "}\n\n")

  fprintf(cg_out, "define internal void @__vb_arena_destroy(ptr %%a) {\n")
  fprintf(cg_out, "entry:\n")
  fprintf(cg_out, "  %%base_slot = getelementptr inbounds %%struct.__vb_arena, ptr %%a, i32 0, i32 0\n")
  fprintf(cg_out, "  %%base = load ptr, ptr %%base_slot, align 8\n")
  fprintf(cg_out, "  %%nonnull = icmp ne ptr %%base, null\n")
  fprintf(cg_out, "  br i1 %%nonnull, label %%do_free, label %%clear\n")
  fprintf(cg_out, "do_free:\n")
  fprintf(cg_out, "  call void @free(ptr %%base)\n")
  fprintf(cg_out, "  br label %%clear\n")
  fprintf(cg_out, "clear:\n")
  fprintf(cg_out, "  store %%struct.__vb_arena zeroinitializer, ptr %%a, align 8\n")
  fprintf(cg_out, "  ret void\n")
  fprintf(cg_out, "}\n\n")
End Sub

Private Function CgHasExtern(name As Pointer, nLen As Integer) As Boolean
  Dim i As Integer = 0
  While i < cg_funcCount Do
    If (cg_funcs(i).isExtern = 1) And SliceEqualsStr(cg_funcs(i).namePos, cg_funcs(i).nameLen, name, nLen) Then
      Return True
    End If
    i = i + 1
  End While
  Return False
End Function

Public Function EmitProgramToFile(ByRef arena As Arena, progRoot As Integer, outPath As Pointer) As Integer
  cg_enums = ArenaAlloc(ByRef arena, CgEnumInfo, 64)
  cg_enumMems = ArenaAlloc(ByRef arena, CgEnumMem, 512)
  cg_structs = ArenaAlloc(ByRef arena, CgStructInfo, 64)
  cg_fields = ArenaAlloc(ByRef arena, CgFieldInfo, 512)
  cg_globals = ArenaAlloc(ByRef arena, CgGlobalInfo, 256)
  cg_funcs = ArenaAlloc(ByRef arena, CgFuncInfo, 256)
  cg_params = ArenaAlloc(ByRef arena, CgParamInfo, 1024)
  cg_locals = ArenaAlloc(ByRef arena, CgLocalSym, 256)
  cg_strs = ArenaAlloc(ByRef arena, CgStrConst, 512)
  cg_staticLocals = ArenaAlloc(ByRef arena, CgStaticLocal, 256)

  cg_enumCount = 0
  cg_enumMemCount = 0
  cg_structCount = 0
  cg_fieldCount = 0
  cg_globalCount = 0
  cg_funcCount = 0
  cg_paramCount = 0
  cg_localCount = 0
  cg_strCount = 0
  cg_staticLocalCount = 0
  cg_lbl = 1
  cg_usesArena = 0

  Dim defStart As Integer = g_nodes(progRoot).firstChild
  Dim defCount As Integer = g_nodes(progRoot).childCount

  ' Pass 1a: Register all Enums & Struct headers
  Dim i As Integer = 0
  While i < defCount Do
    Dim d As Integer = g_kids(defStart + i)
    Dim dk As Integer = g_nodes(d).kind
    If dk = NodeKind.NdEnumDef Then
      Dim ms As Integer = cg_enumMemCount
      Dim mc As Integer = g_nodes(d).childCount
      Dim j As Integer = 0
      While j < mc Do
        Dim m As Integer = g_kids(g_nodes(d).firstChild + j)
        cg_enumMems(cg_enumMemCount) = CgEnumMem With {
          namePos = g_nodes(m).intVal,
          nameLen = g_nodes(m).lenVal,
          value = g_nodes(m).op
        }
        cg_enumMemCount = cg_enumMemCount + 1
        j = j + 1
      End While
      cg_enums(cg_enumCount) = CgEnumInfo With {
        namePos = g_nodes(d).intVal,
        nameLen = g_nodes(d).lenVal,
        memStart = ms,
        memCount = mc
      }
      cg_enumCount = cg_enumCount + 1
    End If
    If dk = NodeKind.NdStructDef Then
      cg_structs(cg_structCount) = CgStructInfo With {
        namePos = g_nodes(d).intVal,
        nameLen = g_nodes(d).lenVal,
        fieldStart = 0,
        fieldCount = 0
      }
      cg_structCount = cg_structCount + 1
    End If
    i = i + 1
  End While

  ' Pass 1b: Resolve Struct fields (now that all structs & enums are registered)
  Dim si As Integer = 0
  i = 0
  While i < defCount Do
    Dim d As Integer = g_kids(defStart + i)
    If g_nodes(d).kind = NodeKind.NdStructDef Then
      Dim fs As Integer = cg_fieldCount
      Dim fc As Integer = g_nodes(d).childCount
      Dim j As Integer = 0
      While j < fc Do
        Dim f As Integer = g_kids(g_nodes(d).firstChild + j)
        cg_fields(cg_fieldCount) = CgFieldInfo With {
          namePos = g_nodes(f).intVal,
          nameLen = g_nodes(f).lenVal,
          ty = CgResolveType(g_nodes(f).tyKind, g_nodes(f).tyArrLen, g_nodes(f).tyNamePos, g_nodes(f).tyNameLen)
        }
        cg_fieldCount = cg_fieldCount + 1
        j = j + 1
      End While
      cg_structs(si).fieldStart = fs
      cg_structs(si).fieldCount = fc
      si = si + 1
    End If
    i = i + 1
  End While

  ' Pass 1c: Register Globals, Functions, and ExternDecls
  i = 0
  While i < defCount Do
    Dim d As Integer = g_kids(defStart + i)
    Dim dk As Integer = g_nodes(d).kind
    If dk = NodeKind.NdGlobalVar Then
      Dim gvPub As Integer = 0
      Dim gvPriv As Integer = 0
      If g_nodes(d).op = 1 Then
        gvPub = 1
      End If
      If g_nodes(d).op = 2 Then
        gvPriv = 1
      End If
      cg_globals(cg_globalCount) = CgGlobalInfo With {
        namePos = g_nodes(d).intVal,
        nameLen = g_nodes(d).lenVal,
        isPublic = gvPub,
        isPrivate = gvPriv,
        modId = g_nodes(d).modId,
        ty = CgResolveType(g_nodes(d).tyKind, g_nodes(d).tyArrLen, g_nodes(d).tyNamePos, g_nodes(d).tyNameLen),
        initNode = g_nodes(d).left
      }
      cg_globalCount = cg_globalCount + 1
    End If
    If (dk = NodeKind.NdFuncDef) Or (dk = NodeKind.NdExternDecl) Then
      Dim ps As Integer = cg_paramCount
      Dim pc As Integer = g_nodes(d).extraCount
      Dim j As Integer = 0
      While j < pc Do
        Dim pn As Integer = g_kids(g_nodes(d).extraStart + j)
        cg_params(cg_paramCount) = CgParamInfo With {
          isByRef = g_nodes(pn).op,
          namePos = g_nodes(pn).intVal,
          nameLen = g_nodes(pn).lenVal,
          ty = CgResolveType(g_nodes(pn).tyKind, g_nodes(pn).tyArrLen, g_nodes(pn).tyNamePos, g_nodes(pn).tyNameLen)
        }
        cg_paramCount = cg_paramCount + 1
        j = j + 1
      End While
      Dim isExt As Integer = 0
      Dim isVar As Integer = 0
      Dim isPub As Integer = 0
      Dim isPriv As Integer = 0
      If dk = NodeKind.NdExternDecl Then
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
      End If
      ' Avoid duplicate extern declarations
      cg_curModId = g_nodes(d).modId
      Dim existing As Integer = CgFindFunc(g_nodes(d).intVal, g_nodes(d).lenVal)
      If (isExt = 0) Or (existing < 0) Then
        g_nodes(d).third = cg_funcCount
        cg_funcs(cg_funcCount) = CgFuncInfo With {
          namePos = g_nodes(d).intVal,
          nameLen = g_nodes(d).lenVal,
          isExtern = isExt,
          isVariadic = isVar,
          isPublic = isPub,
          isPrivate = isPriv,
          modId = g_nodes(d).modId,
          retTy = CgResolveType(g_nodes(d).tyKind, 0, g_nodes(d).tyNamePos, g_nodes(d).tyNameLen),
          paramStart = ps,
          paramCount = pc
        }
        cg_funcCount = cg_funcCount + 1
      End If
    End If
    i = i + 1
  End While

  ' Collect all strings and detect Arena usage
  CgCollectStrings(progRoot)

  cg_out = fopen(outPath, "w")
  If IsNull(cg_out) Then
    printf("vbc codegen error: cannot open output file\n")
    Return -1
  End If

  fprintf(cg_out, "; Generated by vbc (VerBose Self-Hosting Compiler)\n\n")

  If cg_usesArena = 1 Then
    fprintf(cg_out, "%%struct.__vb_arena = type { ptr, i64, i64 }\n\n")
  End If

  ' Emit struct layouts
  i = 0
  While i < cg_structCount Do
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
    i = i + 1
  End While
  fprintf(cg_out, "\n")

  ' Emit Static globals
  i = 0
  While i < cg_globalCount Do
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
    fprintf(cg_out, ", align 8\n")
    i = i + 1
  End While
  fprintf(cg_out, "\n")

  ' Emit function-local Static variables
  i = 0
  While i < cg_staticLocalCount Do
    fprintf(cg_out, "@__vb_static_%d = internal global ", i)
    CgEmitLlvmType(cg_staticLocals(i).ty)
    fprintf(cg_out, " ")
    CgEmitConstInit(cg_staticLocals(i).ty, cg_staticLocals(i).initNode)
    fprintf(cg_out, ", align 8\n")
    i = i + 1
  End While
  fprintf(cg_out, "\n")

  ' Emit string literal globals
  CgEmitStringGlobals()
  fprintf(cg_out, "\n")

  ' Emit default C runtime declarations (skipping any overridden by Declare)
  If Not CgHasExtern("printf", 6) Then
    fprintf(cg_out, "declare i32 @printf(ptr, ...)\n")
  End If
  If Not CgHasExtern("malloc", 6) Then
    fprintf(cg_out, "declare ptr @malloc(i32)\n")
  End If
  If Not CgHasExtern("calloc", 6) Then
    fprintf(cg_out, "declare ptr @calloc(i64, i64)\n")
  End If
  If Not CgHasExtern("realloc", 7) Then
    fprintf(cg_out, "declare ptr @realloc(ptr, i64)\n")
  End If
  If Not CgHasExtern("free", 4) Then
    fprintf(cg_out, "declare void @free(ptr)\n")
  End If
  If Not CgHasExtern("fopen", 5) Then
    fprintf(cg_out, "declare ptr @fopen(ptr, ptr)\n")
  End If
  If Not CgHasExtern("getc", 4) Then
    fprintf(cg_out, "declare i32 @getc(ptr)\n")
  End If
  If Not CgHasExtern("putc", 4) Then
    fprintf(cg_out, "declare i32 @putc(i32, ptr)\n")
  End If

  ' Emit user Declare externs
  i = 0
  While i < cg_funcCount Do
    If cg_funcs(i).isExtern = 1 Then
      fprintf(cg_out, "declare ")
      CgEmitLlvmType(cg_funcs(i).retTy)
      fprintf(cg_out, " @")
      CgEmitSlice(cg_funcs(i).namePos, cg_funcs(i).nameLen)
      fprintf(cg_out, "(")
      Dim j As Integer = 0
      While j < cg_funcs(i).paramCount Do
        If j > 0 Then
          fprintf(cg_out, ", ")
        End If
        CgEmitParamLlvmType(cg_params(cg_funcs(i).paramStart + j))
        j = j + 1
      End While
      If cg_funcs(i).isVariadic = 1 Then
        If cg_funcs(i).paramCount > 0 Then
          fprintf(cg_out, ", ...")
        Else
          fprintf(cg_out, "...")
        End If
      End If
      fprintf(cg_out, ")\n")
    End If
    i = i + 1
  End While

  fprintf(cg_out, "declare void @llvm.memcpy.p0.p0.i64(ptr nocapture writeonly, ptr nocapture readonly, i64, i1 immarg)\n\n")
  If cg_usesArena = 1 Then
    CgEmitArenaRuntime()
  End If

  ' Emit all function bodies
  i = 0
  While i < defCount Do
    Dim d As Integer = g_kids(defStart + i)
    If g_nodes(d).kind = NodeKind.NdFuncDef Then
      CgEmitFunc(d, g_nodes(d).third)
    End If
    i = i + 1
  End While

  fclose(cg_out)
  Return 0
End Function
