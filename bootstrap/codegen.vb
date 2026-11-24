' VerBose Bootstrap Compiler (vbc) - LLVM IR Code Generator written in VerBose-Lang

Include "parser.vb"

Declare Function fclose(fp As Pointer) As Integer
Declare Function fprintf(fp As Pointer, fmt As Pointer, ...) As Integer
Declare Function fputc(c As Integer, fp As Pointer) As Integer

Structure LocalSym
  Dim namePos As Integer
  Dim nameLen As Integer
  Dim slotId As Integer
End Structure

Structure StrConst
  Dim srcPos As Integer
  Dim srcLen As Integer
  Dim byteLen As Integer
End Structure

Private Static cg_out As Pointer
Private Static cg_reg As Integer = 1
Private Static cg_lbl As Integer = 1
Private Static cg_locals[64] As LocalSym
Private Static cg_localCount As Integer = 0
Private Static cg_strs[128] As StrConst
Private Static cg_strCount As Integer = 0
Private Static cg_progRoot As Integer = -1

Private Function NextReg() As Integer
  Dim r As Integer = cg_reg
  cg_reg = cg_reg + 1
  Return r
End Function

Private Function NextLbl() As Integer
  Dim l As Integer = cg_lbl
  cg_lbl = cg_lbl + 1
  Return l
End Function

Private Sub EmitSlice(pos As Integer, slen As Integer)
  Dim i As Integer = 0
  While i < slen Do
    Dim ch As Integer = g_src[pos + i]
    fputc(ch, cg_out)
    i = i + 1
  End While
End Sub

Private Function FindLocal(pos As Integer, slen As Integer) As Integer
  Dim i As Integer = 0
  While i < cg_localCount Do
    If SlicesEqual(cg_locals[i].namePos, cg_locals[i].nameLen, pos, slen) Then
      Return cg_locals[i].slotId
    End If
    i = i + 1
  End While
  Return -1
End Function

Private Function AddLocal(pos As Integer, slen As Integer) As Integer
  Dim slot As Integer = NextReg()
  cg_locals[cg_localCount] = LocalSym With {
    namePos = pos,
    nameLen = slen,
    slotId = slot
  }
  cg_localCount = cg_localCount + 1
  Return slot
End Function

Private Function IsSubFunc(pos As Integer, slen As Integer) As Boolean
  Dim fnStart As Integer = g_nodes[cg_progRoot].firstChild
  Dim fnCount As Integer = g_nodes[cg_progRoot].childCount
  Dim i As Integer = 0
  While i < fnCount Do
    Dim fnId As Integer = g_kids[fnStart + i]
    If SlicesEqual(g_nodes[fnId].intVal, g_nodes[fnId].lenVal, pos, slen) Then
      Dim isSub As Integer = g_nodes[fnId].op / 2
      Return isSub = 1
    End If
    i = i + 1
  End While
  Return False
End Function

' Compute unescaped byte count and register string literal for header emission
Private Function RegisterString(srcPos As Integer, srcLen As Integer) As Integer
  Dim idx As Integer = cg_strCount
  Dim i As Integer = 0
  Dim bytes As Integer = 0
  While i < srcLen Do
    Dim ch As Integer = g_src[srcPos + i]
    If (ch = 92) And ((i + 1) < srcLen) Then
      bytes = bytes + 1
      i = i + 2
    Else
      bytes = bytes + 1
      i = i + 1
    End If
  End While
  cg_strs[idx] = StrConst With {
    srcPos = srcPos,
    srcLen = srcLen,
    byteLen = bytes + 1
  }
  cg_strCount = cg_strCount + 1
  Return idx
End Function

Private Sub CollectStringsInNode(id As Integer)
  If id < 0 Then
    Return
  End If
  Dim k As Integer = g_nodes[id].kind
  If k = NodeKind.NdStrLit Then
    Dim sid As Integer = RegisterString(g_nodes[id].intVal, g_nodes[id].lenVal)
    g_nodes[id].op = sid
    Return
  End If
  CollectStringsInNode(g_nodes[id].left)
  CollectStringsInNode(g_nodes[id].right)
  Dim i As Integer = 0
  While i < g_nodes[id].childCount Do
    CollectStringsInNode(g_kids[g_nodes[id].firstChild + i])
    i = i + 1
  End While
  Dim j As Integer = 0
  While j < g_nodes[id].extraCount Do
    CollectStringsInNode(g_kids[g_nodes[id].extraStart + j])
    j = j + 1
  End While
End Sub

Private Sub EmitStringGlobals()
  Dim i As Integer = 0
  While i < cg_strCount Do
    Dim sp As Integer = cg_strs[i].srcPos
    Dim sl As Integer = cg_strs[i].srcLen
    Dim bl As Integer = cg_strs[i].byteLen
    fprintf(cg_out, "@.str_%d = private unnamed_addr constant [%d x i8] c\"", i, bl)
    Dim j As Integer = 0
    While j < sl Do
      Dim ch As Integer = g_src[sp + j]
      If (ch = 92) And ((j + 1) < sl) Then
        Dim nc As Integer = g_src[sp + j + 1]
        If nc = 110 Then
          fprintf(cg_out, "\\0A")
        Else
          If nc = 116 Then
            fprintf(cg_out, "\\09")
          Else
            fprintf(cg_out, "\\%02X", nc)
          End If
        End If
        j = j + 2
      Else
        fputc(ch, cg_out)
        j = j + 1
      End If
    End While
    fprintf(cg_out, "\\00\", align 1\n")
    i = i + 1
  End While
End Sub

' Returns LLVM register number (positive) holding i32 (or ptr for NdStrLit when outIsPtr=1)
Private Function EmitExpr(id As Integer, ByRef outIsPtr As Integer) As Integer
  outIsPtr = 0
  Dim k As Integer = g_nodes[id].kind

  If k = NodeKind.NdIntLit Then
    Dim r As Integer = NextReg()
    fprintf(cg_out, "  %%r%d = add i32 0, %d\n", r, g_nodes[id].intVal)
    Return r
  End If

  If k = NodeKind.NdStrLit Then
    outIsPtr = 1
    Return g_nodes[id].op
  End If

  If k = NodeKind.NdVar Then
    Dim slot As Integer = FindLocal(g_nodes[id].intVal, g_nodes[id].lenVal)
    Dim r As Integer = NextReg()
    fprintf(cg_out, "  %%r%d = load i32, ptr %%r%d, align 4\n", r, slot)
    Return r
  End If

  If k = NodeKind.NdNeg Then
    Dim dummy As Integer = 0
    Dim v As Integer = EmitExpr(g_nodes[id].left, ByRef dummy)
    Dim r As Integer = NextReg()
    fprintf(cg_out, "  %%r%d = sub nsw i32 0, %%r%d\n", r, v)
    Return r
  End If

  If k = NodeKind.NdBinOp Then
    Dim d1 As Integer = 0
    Dim d2 As Integer = 0
    Dim v1 As Integer = EmitExpr(g_nodes[id].left, ByRef d1)
    Dim v2 As Integer = EmitExpr(g_nodes[id].right, ByRef d2)
    Dim r As Integer = NextReg()
    Dim op As Integer = g_nodes[id].op
    Select Case op
      Case TokenKind.TkPlus
        fprintf(cg_out, "  %%r%d = add nsw i32 %%r%d, %%r%d\n", r, v1, v2)
      Case TokenKind.TkMinus
        fprintf(cg_out, "  %%r%d = sub nsw i32 %%r%d, %%r%d\n", r, v1, v2)
      Case TokenKind.TkStar
        fprintf(cg_out, "  %%r%d = mul nsw i32 %%r%d, %%r%d\n", r, v1, v2)
      Case TokenKind.TkSlash
        fprintf(cg_out, "  %%r%d = sdiv i32 %%r%d, %%r%d\n", r, v1, v2)
      Case TokenKind.TkEq
        Dim cmp As Integer = NextReg()
        fprintf(cg_out, "  %%r%d = icmp eq i32 %%r%d, %%r%d\n", cmp, v1, v2)
        fprintf(cg_out, "  %%r%d = zext i1 %%r%d to i32\n", r, cmp)
      Case TokenKind.TkNeq
        Dim cmp As Integer = NextReg()
        fprintf(cg_out, "  %%r%d = icmp ne i32 %%r%d, %%r%d\n", cmp, v1, v2)
        fprintf(cg_out, "  %%r%d = zext i1 %%r%d to i32\n", r, cmp)
      Case TokenKind.TkLt
        Dim cmp As Integer = NextReg()
        fprintf(cg_out, "  %%r%d = icmp slt i32 %%r%d, %%r%d\n", cmp, v1, v2)
        fprintf(cg_out, "  %%r%d = zext i1 %%r%d to i32\n", r, cmp)
      Case TokenKind.TkGt
        Dim cmp As Integer = NextReg()
        fprintf(cg_out, "  %%r%d = icmp sgt i32 %%r%d, %%r%d\n", cmp, v1, v2)
        fprintf(cg_out, "  %%r%d = zext i1 %%r%d to i32\n", r, cmp)
      Case TokenKind.TkLe
        Dim cmp As Integer = NextReg()
        fprintf(cg_out, "  %%r%d = icmp sle i32 %%r%d, %%r%d\n", cmp, v1, v2)
        fprintf(cg_out, "  %%r%d = zext i1 %%r%d to i32\n", r, cmp)
      Case TokenKind.TkGe
        Dim cmp As Integer = NextReg()
        fprintf(cg_out, "  %%r%d = icmp sge i32 %%r%d, %%r%d\n", cmp, v1, v2)
        fprintf(cg_out, "  %%r%d = zext i1 %%r%d to i32\n", r, cmp)
      Case Else
        fprintf(cg_out, "  %%r%d = add i32 0, 0\n", r)
    End Select
    Return r
  End If

  If k = NodeKind.NdCall Then
    Dim nPos As Integer = g_nodes[id].intVal
    Dim nLen As Integer = g_nodes[id].lenVal
    Dim aStart As Integer = g_nodes[id].firstChild
    Dim aCount As Integer = g_nodes[id].childCount
    Dim argRegs[32] As Integer
    Dim argPtrs[32] As Integer
    Dim i As Integer = 0
    While i < aCount Do
      Dim isP As Integer = 0
      argRegs[i] = EmitExpr(g_kids[aStart + i], ByRef isP)
      argPtrs[i] = isP
      i = i + 1
    End While

    Dim isPrintf As Boolean = SliceEqualsStr(nPos, nLen, "printf", 6)
    Dim isVoid As Boolean = IsSubFunc(nPos, nLen)
    Dim r As Integer = 0
    If isVoid Then
      fprintf(cg_out, "  call void @")
      EmitSlice(nPos, nLen)
      fprintf(cg_out, "(")
    Else
      r = NextReg()
      If isPrintf Then
        fprintf(cg_out, "  %%r%d = call i32 (ptr, ...) @printf(", r)
      Else
        fprintf(cg_out, "  %%r%d = call i32 @", r)
        EmitSlice(nPos, nLen)
        fprintf(cg_out, "(")
      End If
    End If

    Dim j As Integer = 0
    While j < aCount Do
      If j > 0 Then
        fprintf(cg_out, ", ")
      End If
      If argPtrs[j] = 1 Then
        fprintf(cg_out, "ptr @.str_%d", argRegs[j])
      Else
        fprintf(cg_out, "i32 %%r%d", argRegs[j])
      End If
      j = j + 1
    End While
    fprintf(cg_out, ")\n")
    Return r
  End If

  Return 0
End Function

Private Function BlockEndsReturn(startIdx As Integer, cnt As Integer) As Boolean
  Dim i As Integer = 0
  While i < cnt Do
    Dim sid As Integer = g_kids[startIdx + i]
    If g_nodes[sid].kind = NodeKind.NdReturn Then
      Return True
    End If
    i = i + 1
  End While
  Return False
End Function

Private Sub EmitStmt(id As Integer)
  Dim k As Integer = g_nodes[id].kind

  If k = NodeKind.NdDim Then
    Dim slot As Integer = AddLocal(g_nodes[id].intVal, g_nodes[id].lenVal)
    fprintf(cg_out, "  %%r%d = alloca i32, align 4\n", slot)
    If g_nodes[id].left >= 0 Then
      Dim dummy As Integer = 0
      Dim v As Integer = EmitExpr(g_nodes[id].left, ByRef dummy)
      fprintf(cg_out, "  store i32 %%r%d, ptr %%r%d, align 4\n", v, slot)
    Else
      fprintf(cg_out, "  store i32 0, ptr %%r%d, align 4\n", slot)
    End If
    Return
  End If

  If k = NodeKind.NdAssign Then
    Dim slot As Integer = FindLocal(g_nodes[id].intVal, g_nodes[id].lenVal)
    Dim dummy As Integer = 0
    Dim v As Integer = EmitExpr(g_nodes[id].left, ByRef dummy)
    fprintf(cg_out, "  store i32 %%r%d, ptr %%r%d, align 4\n", v, slot)
    Return
  End If

  If k = NodeKind.NdExprStmt Then
    Dim dummy As Integer = 0
    EmitExpr(g_nodes[id].left, ByRef dummy)
    Return
  End If

  If k = NodeKind.NdReturn Then
    If g_nodes[id].left >= 0 Then
      Dim dummy As Integer = 0
      Dim v As Integer = EmitExpr(g_nodes[id].left, ByRef dummy)
      fprintf(cg_out, "  ret i32 %%r%d\n", v)
    Else
      fprintf(cg_out, "  ret void\n")
    End If
    Return
  End If

  If k = NodeKind.NdIf Then
    Dim dummy As Integer = 0
    Dim condVal As Integer = EmitExpr(g_nodes[id].left, ByRef dummy)
    Dim condBit As Integer = NextReg()
    fprintf(cg_out, "  %%r%d = icmp ne i32 %%r%d, 0\n", condBit, condVal)
    Dim lThen As Integer = NextLbl()
    Dim lElse As Integer = NextLbl()
    Dim lCont As Integer = NextLbl()
    Dim thenRet As Boolean = BlockEndsReturn(g_nodes[id].firstChild, g_nodes[id].childCount)
    Dim elseRet As Boolean = BlockEndsReturn(g_nodes[id].extraStart, g_nodes[id].extraCount)

    fprintf(cg_out, "  br i1 %%r%d, label %%L%d, label %%L%d\n", condBit, lThen, lElse)
    fprintf(cg_out, "L%d:\n", lThen)
    Dim i As Integer = 0
    While i < g_nodes[id].childCount Do
      EmitStmt(g_kids[g_nodes[id].firstChild + i])
      i = i + 1
    End While
    If Not thenRet Then
      fprintf(cg_out, "  br label %%L%d\n", lCont)
    End If

    fprintf(cg_out, "L%d:\n", lElse)
    Dim j As Integer = 0
    While j < g_nodes[id].extraCount Do
      EmitStmt(g_kids[g_nodes[id].extraStart + j])
      j = j + 1
    End While
    If Not elseRet Then
      fprintf(cg_out, "  br label %%L%d\n", lCont)
    End If

    If Not (thenRet And elseRet) Then
      fprintf(cg_out, "L%d:\n", lCont)
    End If
    Return
  End If

  If k = NodeKind.NdWhile Then
    Dim lCond As Integer = NextLbl()
    Dim lBody As Integer = NextLbl()
    Dim lEnd As Integer = NextLbl()
    fprintf(cg_out, "  br label %%L%d\n", lCond)
    fprintf(cg_out, "L%d:\n", lCond)
    Dim dummy As Integer = 0
    Dim condVal As Integer = EmitExpr(g_nodes[id].left, ByRef dummy)
    Dim condBit As Integer = NextReg()
    fprintf(cg_out, "  %%r%d = icmp ne i32 %%r%d, 0\n", condBit, condVal)
    fprintf(cg_out, "  br i1 %%r%d, label %%L%d, label %%L%d\n", condBit, lBody, lEnd)
    fprintf(cg_out, "L%d:\n", lBody)
    Dim i As Integer = 0
    While i < g_nodes[id].childCount Do
      EmitStmt(g_kids[g_nodes[id].firstChild + i])
      i = i + 1
    End While
    fprintf(cg_out, "  br label %%L%d\n", lCond)
    fprintf(cg_out, "L%d:\n", lEnd)
    Return
  End If
End Sub

Private Sub EmitFunc(fnId As Integer)
  cg_reg = 1
  cg_localCount = 0
  Dim isSub As Integer = g_nodes[fnId].op / 2
  Dim isPub As Integer = g_nodes[fnId].op - isSub * 2

  If isPub = 1 Then
    fprintf(cg_out, "define ")
  Else
    fprintf(cg_out, "define internal ")
  End If
  If isSub = 1 Then
    fprintf(cg_out, "void @")
  Else
    fprintf(cg_out, "i32 @")
  End If
  EmitSlice(g_nodes[fnId].intVal, g_nodes[fnId].lenVal)
  fprintf(cg_out, "(")

  Dim pStart As Integer = g_nodes[fnId].extraStart
  Dim pCount As Integer = g_nodes[fnId].extraCount
  Dim i As Integer = 0
  While i < pCount Do
    If i > 0 Then
      fprintf(cg_out, ", ")
    End If
    fprintf(cg_out, "i32 %%p%d", i)
    i = i + 1
  End While
  fprintf(cg_out, ") {\nentry:\n")

  Dim j As Integer = 0
  While j < pCount Do
    Dim pNode As Integer = g_kids[pStart + j]
    Dim slot As Integer = AddLocal(g_nodes[pNode].intVal, g_nodes[pNode].lenVal)
    fprintf(cg_out, "  %%r%d = alloca i32, align 4\n", slot)
    fprintf(cg_out, "  store i32 %%p%d, ptr %%r%d, align 4\n", j, slot)
    j = j + 1
  End While

  Dim bStart As Integer = g_nodes[fnId].firstChild
  Dim bCount As Integer = g_nodes[fnId].childCount
  Dim k As Integer = 0
  While k < bCount Do
    EmitStmt(g_kids[bStart + k])
    k = k + 1
  End While

  If isSub = 1 Then
    fprintf(cg_out, "  ret void\n")
  End If
  fprintf(cg_out, "}\n\n")
End Sub

Public Function EmitProgramToFile(progRoot As Integer, outPath As Pointer) As Integer
  cg_progRoot = progRoot
  cg_strCount = 0
  cg_lbl = 1
  CollectStringsInNode(progRoot)

  cg_out = fopen(outPath, "w")
  If IsNull(cg_out) Then
    printf("vbc codegen error: cannot open output file\n")
    Return -1
  End If

  fprintf(cg_out, "; Generated by vbc (VerBose Bootstrap Compiler written in VerBose-Lang)\n\n")
  EmitStringGlobals()
  fprintf(cg_out, "\ndeclare i32 @printf(ptr, ...)\n\n")

  Dim fnStart As Integer = g_nodes[progRoot].firstChild
  Dim fnCount As Integer = g_nodes[progRoot].childCount
  Dim i As Integer = 0
  While i < fnCount Do
    EmitFunc(g_kids[fnStart + i])
    i = i + 1
  End While

  fclose(cg_out)
  Return 0
End Function
