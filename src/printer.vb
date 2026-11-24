' VerBose Self-Hosting Compiler (verbose) - AST Pretty Printer (--ast)

Include "parser.vb"

Private Sub PrIndent(depth As Integer)
  Dim i As Integer = 0
  While i < depth Do
    Printf("  ")
    i = i + 1
  End While
End Sub

Private Sub PrSlice(pos, slen As Integer)
  Dim i As Integer = 0
  While i < slen Do
    Dim ch As Integer = g_srcPool(pos + i)
    Printf("%c", ch)
    i = i + 1
  End While
End Sub

Private Sub PrTypeEx(tk, innerKind, arrLen, nPos, nLen As Integer)
  Match tk With
    Case TypeKind.TyNothing Then Printf("Nothing")
    Case TypeKind.TyInt Then Printf("Integer")
    Case TypeKind.TyByte Then Printf("Byte")
    Case TypeKind.TyShort Then Printf("Short")
    Case TypeKind.TyLong Then Printf("Long")
    Case TypeKind.TySingle Then Printf("Single")
    Case TypeKind.TyDouble Then Printf("Double")
    Case TypeKind.TyBoolean Then Printf("Boolean")
    Case TypeKind.TyPointer Then Printf("Pointer")
    Case TypeKind.TyArena Then Printf("Arena")
    Case TypeKind.TyTask Then
      If innerKind = TypeKind.TyNothing Then
        Printf("Task")
      Else
        Printf("Task(")
        PrTypeEx(innerKind, TypeKind.TyNothing, 0, nPos, nLen)
        Printf(")")
      End If
    Case TypeKind.TyStruct, TypeKind.TyEnum Then PrSlice(nPos, nLen)
    Case _ Then Printf("Unknown")
  End Match
  Match With
    Case arrLen > 0 Then Printf("(%d)", arrLen)
    Case arrLen = -1 Then Printf("()")
  End Match
End Sub

Private Sub PrType(tk, arrLen, nPos, nLen As Integer)
  PrTypeEx(tk, TypeKind.TyNothing, arrLen, nPos, nLen)
End Sub

Private Sub PrOp(op As Integer)
  Match op With
    Case TokenKind.TkPlus Then Printf("+")
    Case TokenKind.TkMinus Then Printf("-")
    Case TokenKind.TkStar Then Printf("*")
    Case TokenKind.TkSlash Then Printf("/")
    Case TokenKind.TkAmp Then Printf("&")
    Case TokenKind.TkMod Then Printf("Mod")
    Case TokenKind.TkAnd Then Printf("And")
    Case TokenKind.TkOr Then Printf("Or")
    Case TokenKind.TkXor Then Printf("Xor")
    Case TokenKind.TkShl Then Printf("Shl")
    Case TokenKind.TkShr Then Printf("Shr")
    Case TokenKind.TkEq Then Printf("=")
    Case TokenKind.TkNeq Then Printf("<>")
    Case TokenKind.TkLt Then Printf("<")
    Case TokenKind.TkGt Then Printf(">")
    Case TokenKind.TkLe Then Printf("<=")
    Case TokenKind.TkGe Then Printf(">=")
    Case _ Then Printf("op(%d)", op)
  End Match
End Sub

Private Sub PrExpr(id As Integer)
  If id < 0 Then
    Return
  End If
  Dim k As Integer = g_nodes(id).kind
  If k = NodeKind.NdIntLit Then
    Printf("%d", g_nodes(id).intVal)
    Return
  End If
  If k = NodeKind.NdFloatLit Then
    PrSlice(g_nodes(id).intVal, g_nodes(id).lenVal)
    Return
  End If
  If k = NodeKind.NdBoolLit Then
    If g_nodes(id).op <> 0 Then
      Printf("True")
    Else
      Printf("False")
    End If
    Return
  End If
  If k = NodeKind.NdStrLit Then
    Printf("\"")
    PrSlice(g_nodes(id).intVal, g_nodes(id).lenVal)
    Printf("\"")
    Return
  End If
  If k = NodeKind.NdStrInterp Then
    Printf("$\"")
    Dim ii As Integer = 0
    While ii < g_nodes(id).childCount Do
      Dim ch As Integer = g_kids(g_nodes(id).firstChild + ii)
      If g_nodes(ch).kind = NodeKind.NdStrLit Then
        PrSlice(g_nodes(ch).intVal, g_nodes(ch).lenVal)
      Else
        Printf("{")
        PrExpr(ch)
        Printf("}")
      End If
      ii = ii + 1
    End While
    Printf("\"")
    Return
  End If
  If k = NodeKind.NdVar Then
    PrSlice(g_nodes(id).intVal, g_nodes(id).lenVal)
    Return
  End If
  If k = NodeKind.NdWithTarget Then
    Return
  End If
  If k = NodeKind.NdNeg Then
    Printf("(-")
    PrExpr(g_nodes(id).left)
    Printf(")")
    Return
  End If
  If k = NodeKind.NdNot Then
    Printf("(Not ")
    PrExpr(g_nodes(id).left)
    Printf(")")
    Return
  End If
  If k = NodeKind.NdBinOp Then
    Printf("(")
    PrExpr(g_nodes(id).left)
    Printf(" ")
    PrOp(g_nodes(id).op)
    Printf(" ")
    PrExpr(g_nodes(id).right)
    Printf(")")
    Return
  End If
  If k = NodeKind.NdFieldAccess Then
    PrExpr(g_nodes(id).left)
    Printf(".")
    PrSlice(g_nodes(id).intVal, g_nodes(id).lenVal)
    Return
  End If
  If k = NodeKind.NdIndexAccess Then
    PrExpr(g_nodes(id).left)
    Printf("(")
    PrExpr(g_nodes(id).right)
    Printf(")")
    Return
  End If
  If k = NodeKind.NdSizeOf Then
    Printf("SizeOf(")
    PrTypeEx(g_nodes(id).tyKind, g_nodes(id).tyInnerKind, 0, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    Printf(")")
    Return
  End If
  If k = NodeKind.NdHeapAlloc Then
    Printf("HeapAlloc(")
    PrTypeEx(g_nodes(id).tyKind, g_nodes(id).tyInnerKind, 0, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    Printf(", ")
    PrExpr(g_nodes(id).left)
    Printf(")")
    Return
  End If
  If k = NodeKind.NdHeapRealloc Then
    Printf("HeapRealloc(")
    PrExpr(g_nodes(id).left)
    Printf(", ")
    PrTypeEx(g_nodes(id).tyKind, g_nodes(id).tyInnerKind, 0, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    Printf(", ")
    PrExpr(g_nodes(id).right)
    Printf(")")
    Return
  End If
  If k = NodeKind.NdVolatileLoad Then
    Printf("VolatileLoad(")
    PrExpr(g_nodes(id).left)
    Printf(", ")
    PrTypeEx(g_nodes(id).tyKind, g_nodes(id).tyInnerKind, 0, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    Printf(")")
    Return
  End If
  If k = NodeKind.NdVolatileStore Then
    Printf("VolatileStore(")
    PrExpr(g_nodes(id).left)
    Printf(", ")
    PrTypeEx(g_nodes(id).tyKind, g_nodes(id).tyInnerKind, 0, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    Printf(", ")
    PrExpr(g_nodes(id).right)
    Printf(")")
    Return
  End If
  If k = NodeKind.NdAddressOf Then
    Printf("AddressOf(")
    PrExpr(g_nodes(id).left)
    Printf(")")
    Return
  End If
  If k = NodeKind.NdPtrRead Then
    Printf("PtrRead(")
    PrExpr(g_nodes(id).left)
    Printf(", ")
    PrTypeEx(g_nodes(id).tyKind, g_nodes(id).tyInnerKind, 0, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    Printf(")")
    Return
  End If
  If k = NodeKind.NdPtrWrite Then
    Printf("PtrWrite(")
    PrExpr(g_nodes(id).left)
    Printf(", ")
    PrTypeEx(g_nodes(id).tyKind, g_nodes(id).tyInnerKind, 0, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    Printf(", ")
    PrExpr(g_nodes(id).right)
    Printf(")")
    Return
  End If
  If k = NodeKind.NdPtrAdd Then
    Printf("PtrAdd(")
    PrExpr(g_nodes(id).left)
    Printf(", ")
    PrExpr(g_nodes(id).right)
    Printf(")")
    Return
  End If
  If k = NodeKind.NdAwait Then
    Printf("(Await ")
    PrExpr(g_nodes(id).left)
    Printf(")")
    Return
  End If
  If k = NodeKind.NdArenaAlloc Then
    Printf("ArenaAlloc(")
    If g_nodes(id).op = 1 Then
      Printf("ByRef ")
    End If
    PrExpr(g_nodes(id).left)
    Printf(", ")
    PrType(g_nodes(id).tyKind, 0, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    Printf(", ")
    PrExpr(g_nodes(id).right)
    Printf(")")
    Return
  End If
  If k = NodeKind.NdStructInit Then
    PrSlice(g_nodes(id).intVal, g_nodes(id).lenVal)
    Printf(" With ( ")
    Dim i As Integer = 0
    While i < g_nodes(id).childCount Do
      If i > 0 Then
        Printf(", ")
      End If
      Dim fi As Integer = g_kids(g_nodes(id).firstChild + i)
      PrSlice(g_nodes(fi).intVal, g_nodes(fi).lenVal)
      Printf(" = ")
      PrExpr(g_nodes(fi).left)
      i = i + 1
    End While
    Printf(" )")
    Return
  End If
  If k = NodeKind.NdArrayLit Then
    Printf("(")
    Dim i As Integer = 0
    While i < g_nodes(id).childCount Do
      If i > 0 Then
        Printf(", ")
      End If
      PrExpr(g_kids(g_nodes(id).firstChild + i))
      i = i + 1
    End While
    Printf(")")
    Return
  End If
  If k = NodeKind.NdCall Then
    PrSlice(g_nodes(id).intVal, g_nodes(id).lenVal)
    Printf("(")
    Dim i As Integer = 0
    While i < g_nodes(id).childCount Do
      If i > 0 Then
        Printf(", ")
      End If
      Dim aNode As Integer = g_kids(g_nodes(id).firstChild + i)
      If g_nodes(aNode).op = 1 Then
        Printf("ByRef ")
      End If
      PrExpr(g_nodes(aNode).left)
      i = i + 1
    End While
    Printf(")")
    Return
  End If
End Sub

Private Sub PrStmt(id, depth As Integer)
  Dim k As Integer = g_nodes(id).kind
  PrIndent(depth)
  If (k = NodeKind.NdDim) Or (k = NodeKind.NdStaticDim) Then
    If g_nodes(id).flags = 1 Then
      Printf("Volatile ")
    End If
    If k = NodeKind.NdStaticDim Then
      Printf("Static ")
    Else
      Printf("Dim ")
    End If
    PrSlice(g_nodes(id).intVal, g_nodes(id).lenVal)
    Printf(" As ")
    PrTypeEx(g_nodes(id).tyKind, g_nodes(id).tyInnerKind, g_nodes(id).tyArrLen, g_nodes(id).tyNamePos, g_nodes(id).tyNameLen)
    If g_nodes(id).right >= 0 Then
      Printf(" At ")
      PrExpr(g_nodes(id).right)
    End If
    If g_nodes(id).left >= 0 Then
      Printf(" = ")
      PrExpr(g_nodes(id).left)
    End If
    Printf("\n")
    Return
  End If
  If k = NodeKind.NdAssign Then
    PrSlice(g_nodes(id).intVal, g_nodes(id).lenVal)
    Printf(" = ")
    PrExpr(g_nodes(id).left)
    Printf("\n")
    Return
  End If
  If k = NodeKind.NdFieldAssign Then
    PrExpr(g_nodes(id).left)
    Printf(".")
    PrSlice(g_nodes(id).intVal, g_nodes(id).lenVal)
    Printf(" = ")
    PrExpr(g_nodes(id).right)
    Printf("\n")
    Return
  End If
  If k = NodeKind.NdIndexAssign Then
    PrExpr(g_nodes(id).left)
    Printf("(")
    PrExpr(g_nodes(id).right)
    Printf(") = ")
    PrExpr(g_nodes(id).third)
    Printf("\n")
    Return
  End If
  If k = NodeKind.NdExprStmt Then
    PrExpr(g_nodes(id).left)
    Printf("\n")
    Return
  End If
  If k = NodeKind.NdReturn Then
    Printf("Return")
    If g_nodes(id).left >= 0 Then
      Printf(" ")
      PrExpr(g_nodes(id).left)
    End If
    Printf("\n")
    Return
  End If
  If k = NodeKind.NdLabel Then
    Printf("Label ")
    PrSlice(g_nodes(id).intVal, g_nodes(id).lenVal)
    Printf("\n")
    Return
  End If
  If k = NodeKind.NdGoTo Then
    Printf("GoTo ")
    PrSlice(g_nodes(id).intVal, g_nodes(id).lenVal)
    Printf("\n")
    Return
  End If
  If k = NodeKind.NdAssert Then
    Printf("Assert ")
    PrExpr(g_nodes(id).left)
    If g_nodes(id).right >= 0 Then
      Printf(", ")
      PrExpr(g_nodes(id).right)
    End If
    Printf("\n")
    Return
  End If
  If k = NodeKind.NdIf Then
    Printf("If ")
    PrExpr(g_nodes(id).left)
    Printf(" Then\n")
    Dim i As Integer = 0
    While i < g_nodes(id).childCount Do
      PrStmt(g_kids(g_nodes(id).firstChild + i), depth + 1)
      i = i + 1
    End While
    If g_nodes(id).extraCount > 0 Then
      PrIndent(depth)
      Printf("Else\n")
      i = 0
      While i < g_nodes(id).extraCount Do
        PrStmt(g_kids(g_nodes(id).extraStart + i), depth + 1)
        i = i + 1
      End While
    End If
    PrIndent(depth)
    Printf("End If\n")
    Return
  End If
  If k = NodeKind.NdWhile Then
    Printf("While ")
    PrExpr(g_nodes(id).left)
    Printf(" Do\n")
    Dim i As Integer = 0
    While i < g_nodes(id).childCount Do
      PrStmt(g_kids(g_nodes(id).firstChild + i), depth + 1)
      i = i + 1
    End While
    PrIndent(depth)
    Printf("End While\n")
    Return
  End If
  If k = NodeKind.NdFor Then
    Printf("For ")
    PrSlice(g_nodes(id).intVal, g_nodes(id).lenVal)
    Printf(" = ")
    PrExpr(g_nodes(id).left)
    Printf(" To ")
    PrExpr(g_nodes(id).right)
    Printf(" Do\n")
    Dim i As Integer = 0
    While i < g_nodes(id).childCount Do
      PrStmt(g_kids(g_nodes(id).firstChild + i), depth + 1)
      i = i + 1
    End While
    PrIndent(depth)
    Printf("End For\n")
    Return
  End If
  If k = NodeKind.NdWith Then
    Printf("With ")
    PrExpr(g_nodes(id).left)
    Printf("\n")
    Dim wi As Integer = 0
    While wi < g_nodes(id).childCount Do
      PrStmt(g_kids(g_nodes(id).firstChild + wi), depth + 1)
      wi = wi + 1
    End While
    PrIndent(depth)
    Printf("End With\n")
    Return
  End If
  If k = NodeKind.NdMatch Then
    If g_nodes(id).op = 1 Then
      Printf("Match With\n")
    Else
      Printf("Match ")
      PrExpr(g_nodes(id).left)
      Printf(" With\n")
    End If
    Dim bi As Integer = 0
    While bi < g_nodes(id).childCount Do
      Dim arm As Integer = g_kids(g_nodes(id).firstChild + bi)
      PrIndent(depth + 1)
      If g_nodes(arm).op = 1 Then
        Printf("Case _")
      Else
        Printf("Case ")
        Dim vi As Integer = 0
        While vi < g_nodes(arm).extraCount Do
          If vi > 0 Then
            Printf(", ")
          End If
          PrExpr(g_kids(g_nodes(arm).extraStart + vi))
          vi = vi + 1
        End While
      End If
      If g_nodes(arm).left >= 0 Then
        Printf(" When ")
        PrExpr(g_nodes(arm).left)
      End If
      Printf(" Then\n")
      Dim si As Integer = 0
      While si < g_nodes(arm).childCount Do
        PrStmt(g_kids(g_nodes(arm).firstChild + si), depth + 2)
        si = si + 1
      End While
      bi = bi + 1
    End While
    PrIndent(depth)
    Printf("End Match\n")
    Return
  End If
End Sub

Public Sub PrintProgramAst(progRoot As Integer)
  Dim defStart = g_nodes(progRoot).firstChild, defCount = g_nodes(progRoot).childCount, i = 0 As Integer
  While i < defCount Do
    Dim d As Integer = g_kids(defStart + i)
    Dim dk As Integer = g_nodes(d).kind
    If dk = NodeKind.NdEnumDef Then
      Printf("Enum ")
      PrSlice(g_nodes(d).intVal, g_nodes(d).lenVal)
      Printf("\n")
      Dim j As Integer = 0
      While j < g_nodes(d).childCount Do
        Dim m As Integer = g_kids(g_nodes(d).firstChild + j)
        Printf("  ")
        PrSlice(g_nodes(m).intVal, g_nodes(m).lenVal)
        Printf(" = %d\n", g_nodes(m).op)
        j = j + 1
      End While
      Printf("End Enum\n\n")
    End If
    If dk = NodeKind.NdStructDef Then
      Printf("Structure ")
      PrSlice(g_nodes(d).intVal, g_nodes(d).lenVal)
      Printf("\n")
      Dim j As Integer = 0
      While j < g_nodes(d).childCount Do
        Dim f As Integer = g_kids(g_nodes(d).firstChild + j)
        Printf("  Dim ")
        PrSlice(g_nodes(f).intVal, g_nodes(f).lenVal)
        Printf(" As ")
        PrType(g_nodes(f).tyKind, g_nodes(f).tyArrLen, g_nodes(f).tyNamePos, g_nodes(f).tyNameLen)
        Printf("\n")
        j = j + 1
      End While
      Printf("End Structure\n\n")
    End If
    If dk = NodeKind.NdGlobalVar Then
      If g_nodes(d).op = 1 Then
        Printf("Public ")
      Else
        If g_nodes(d).op = 2 Then
          Printf("Private ")
        End If
      End If
      If g_nodes(d).flags = 1 Then
        Printf("Volatile ")
      End If
      Printf("Static ")
      PrSlice(g_nodes(d).intVal, g_nodes(d).lenVal)
      Printf(" As ")
      PrType(g_nodes(d).tyKind, g_nodes(d).tyArrLen, g_nodes(d).tyNamePos, g_nodes(d).tyNameLen)
      If g_nodes(d).right >= 0 Then
        Printf(" At ")
        PrExpr(g_nodes(d).right)
      End If
      If g_nodes(d).left >= 0 Then
        Printf(" = ")
        PrExpr(g_nodes(d).left)
      End If
      Printf("\n\n")
    End If
    If dk = NodeKind.NdExternDecl Then
      Printf("Declare ")
      If g_nodes(d).tyKind = TypeKind.TyNothing Then
        Printf("Sub ")
      Else
        Printf("Function ")
      End If
      PrSlice(g_nodes(d).intVal, g_nodes(d).lenVal)
      Printf("(")
      Dim j As Integer = 0
      While j < g_nodes(d).extraCount Do
        If j > 0 Then
          Printf(", ")
        End If
        Dim pn As Integer = g_kids(g_nodes(d).extraStart + j)
        If g_nodes(pn).op = 1 Then
          Printf("ByRef ")
        End If
        PrSlice(g_nodes(pn).intVal, g_nodes(pn).lenVal)
        Printf(" As ")
        PrType(g_nodes(pn).tyKind, g_nodes(pn).tyArrLen, g_nodes(pn).tyNamePos, g_nodes(pn).tyNameLen)
        j = j + 1
      End While
      If g_nodes(d).op = 1 Then
        If g_nodes(d).extraCount > 0 Then
          Printf(", ...")
        Else
          Printf("...")
        End If
      End If
      Printf(")")
      If g_nodes(d).tyKind <> TypeKind.TyNothing Then
        Printf(" As ")
        PrType(g_nodes(d).tyKind, 0, g_nodes(d).tyNamePos, g_nodes(d).tyNameLen)
      End If
      Printf("\n\n")
    End If
    If dk = NodeKind.NdFuncDef Then
      If g_nodes(d).op = 1 Then
        Printf("Public ")
      Else
        If g_nodes(d).op = 2 Then
          Printf("Private ")
        End If
      End If
      If g_nodes(d).flags = 1 Then
        Printf("Async ")
      End If
      If g_nodes(d).tyKind = TypeKind.TyNothing Then
        Printf("Sub ")
      Else
        Printf("Function ")
      End If
      PrSlice(g_nodes(d).intVal, g_nodes(d).lenVal)
      Printf("(")
      Dim j As Integer = 0
      While j < g_nodes(d).extraCount Do
        If j > 0 Then
          Printf(", ")
        End If
        Dim pn As Integer = g_kids(g_nodes(d).extraStart + j)
        If g_nodes(pn).op = 1 Then
          Printf("ByRef ")
        End If
        PrSlice(g_nodes(pn).intVal, g_nodes(pn).lenVal)
        Printf(" As ")
        PrTypeEx(g_nodes(pn).tyKind, g_nodes(pn).tyInnerKind, g_nodes(pn).tyArrLen, g_nodes(pn).tyNamePos, g_nodes(pn).tyNameLen)
        j = j + 1
      End While
      Printf(")")
      If g_nodes(d).tyKind <> TypeKind.TyNothing Then
        Printf(" As ")
        PrTypeEx(g_nodes(d).tyKind, g_nodes(d).tyInnerKind, 0, g_nodes(d).tyNamePos, g_nodes(d).tyNameLen)
      End If
      Printf("\n")
      j = 0
      While j < g_nodes(d).childCount Do
        PrStmt(g_kids(g_nodes(d).firstChild + j), 1)
        j = j + 1
      End While
      If g_nodes(d).tyKind = TypeKind.TyNothing Then
        Printf("End Sub\n\n")
      Else
        Printf("End Function\n\n")
      End If
    End If
    If dk = NodeKind.NdTestDef Then
      Printf("Test \"")
      PrSlice(g_nodes(d).intVal, g_nodes(d).lenVal)
      Printf("\"\n")
      Dim j As Integer = 0
      While j < g_nodes(d).childCount Do
        PrStmt(g_kids(g_nodes(d).firstChild + j), 1)
        j = j + 1
      End While
      Printf("End Test\n\n")
    End If
    i = i + 1
  End While
End Sub
