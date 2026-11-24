' VerBose Self-Hosting Compiler (verbose) - Full Recursive-Descent Parser

Include "constants.vb"
Include "lexer.vb"
Include "ast.vb"

Private Static p_pos = 0, p_errors = 0 As Integer

Private Function PPeekKind() As Integer
  If p_pos >= g_tokCount Then
    Return TokenKind.TkEof
  End If
  Return g_tokens(p_pos).kind
End Function

Private Function PPeekNextKind() As Integer
  If (p_pos + 1) >= g_tokCount Then
    Return TokenKind.TkEof
  End If
  Return g_tokens(p_pos + 1).kind
End Function

Private Sub PAdvance()
  If p_pos < g_tokCount Then
    p_pos = p_pos + 1
  End If
End Sub

Private Sub PSkipNewlines()
  While PPeekKind() = TokenKind.TkNewline Do
    PAdvance()
  End While
End Sub

Private Sub PExpect(k As Integer)
  If PPeekKind() <> k Then
    Printf(MSG_PARSE_EXPECT, k, PPeekKind(), g_tokens(p_pos).line)
    p_errors = p_errors + 1
  End If
  PAdvance()
End Sub

Private Sub PParseBaseType(ByRef outKind, outNamePos, outNameLen As Integer)
  outNamePos = 0
  outNameLen = 0
  Dim k As Integer = PPeekKind()
  Select Case k
    Case TokenKind.TkInteger
      outKind = TypeKind.TyInt
      PAdvance()
    Case TokenKind.TkByte
      outKind = TypeKind.TyByte
      PAdvance()
    Case TokenKind.TkShort
      outKind = TypeKind.TyShort
      PAdvance()
    Case TokenKind.TkLong
      outKind = TypeKind.TyLong
      PAdvance()
    Case TokenKind.TkBoolean
      outKind = TypeKind.TyBoolean
      PAdvance()
    Case TokenKind.TkPointer, TokenKind.TkString
      outKind = TypeKind.TyPointer
      PAdvance()
    Case TokenKind.TkArena
      outKind = TypeKind.TyArena
      PAdvance()
    Case TokenKind.TkNothing
      outKind = TypeKind.TyNothing
      PAdvance()
    Case TokenKind.TkSingle
      outKind = TypeKind.TySingle
      PAdvance()
    Case TokenKind.TkDouble
      outKind = TypeKind.TyDouble
      PAdvance()
    Case TokenKind.TkIdent
      outKind = TypeKind.TyStruct
      outNamePos = g_tokens(p_pos).pos
      outNameLen = g_tokens(p_pos).len
      PAdvance()
    Case Else
      Printf(MSG_PARSE_EXPECT_TYPE, k, g_tokens(p_pos).line)
      p_errors = p_errors + 1
      outKind = TypeKind.TyInt
      PAdvance()
  End Select
End Sub

Private Sub PParseTypeSpec(ByRef outKind, outInnerKind, outNamePos, outNameLen As Integer)
  outInnerKind = TypeKind.TyNothing
  outNamePos = 0
  outNameLen = 0
  If PPeekKind() = TokenKind.TkTask Then
    outKind = TypeKind.TyTask
    PAdvance()
    If PPeekKind() = TokenKind.TkLParen Then
      PAdvance()
      PParseBaseType(ByRef outInnerKind, ByRef outNamePos, ByRef outNameLen)
      PExpect(TokenKind.TkRParen)
    End If
    Return
  End If
  PParseBaseType(ByRef outKind, ByRef outNamePos, ByRef outNameLen)
End Sub

Private Function PParseCallArg() As Integer
  Dim isByRef As Integer = 0
  If PPeekKind() = TokenKind.TkByRef Then
    isByRef = 1
    PAdvance()
  Else
    If PPeekKind() = TokenKind.TkByVal Then
      PAdvance()
    End If
  End If
  Dim eId As Integer = ParseExpr()
  Dim argId As Integer = NewNode(NodeKind.NdArg)
  g_nodes(argId).op = isByRef
  g_nodes(argId).left = eId
  Return argId
End Function

Private Function PParseWithInit(structNamePos, structNameLen As Integer) As Integer
  PExpect(TokenKind.TkWith)
  PExpect(TokenKind.TkLParen)
  PSkipNewlines()
  Dim fiBuf(64) As Integer, fiCount = 0 As Integer
  While (PPeekKind() <> TokenKind.TkRParen) And (PPeekKind() <> TokenKind.TkEof) Do
    Dim fPos = g_tokens(p_pos).pos, fLen = g_tokens(p_pos).len As Integer
    PExpect(TokenKind.TkIdent)
    PExpect(TokenKind.TkEq)
    Dim valExpr As Integer = ParseExpr()
    Dim fiId As Integer = NewNode(NodeKind.NdFieldInit)
    g_nodes(fiId).intVal = fPos
    g_nodes(fiId).lenVal = fLen
    g_nodes(fiId).left = valExpr
    fiBuf(fiCount) = fiId
    fiCount = fiCount + 1
    PSkipNewlines()
    If PPeekKind() = TokenKind.TkComma Then
      PAdvance()
      PSkipNewlines()
    End If
  End While
  PExpect(TokenKind.TkRParen)

  Dim initId As Integer = NewNode(NodeKind.NdStructInit)
  g_nodes(initId).intVal = structNamePos
  g_nodes(initId).lenVal = structNameLen
  g_nodes(initId).firstChild = g_kidCount
  g_nodes(initId).childCount = fiCount
  Dim i As Integer = 0
  While i < fiCount Do
    PushKid(fiBuf(i))
    i = i + 1
  End While
  Return initId
End Function

Private Function PParseArrayLit() As Integer
  PExpect(TokenKind.TkLParen)
  PSkipNewlines()
  Dim elBuf(64) As Integer
  Dim elCount As Integer = 0
  If PPeekKind() <> TokenKind.TkRParen Then
    Dim reading As Boolean = True
    While reading Do
      elBuf(elCount) = ParseExpr()
      elCount = elCount + 1
      PSkipNewlines()
      If PPeekKind() = TokenKind.TkComma Then
        PAdvance()
        PSkipNewlines()
      Else
        reading = False
      End If
    End While
  End If
  PExpect(TokenKind.TkRParen)
  Dim litId As Integer = NewNode(NodeKind.NdArrayLit)
  g_nodes(litId).firstChild = g_kidCount
  g_nodes(litId).childCount = elCount
  Dim i As Integer = 0
  While i < elCount Do
    PushKid(elBuf(i))
    i = i + 1
  End While
  Return litId
End Function

Private Function PParsePrimary() As Integer
  Dim k As Integer = PPeekKind()

  If k = TokenKind.TkIntLit Then
    Dim id As Integer = NewNode(NodeKind.NdIntLit)
    g_nodes(id).intVal = g_tokens(p_pos).intVal
    PAdvance()
    Return id
  End If

  If k = TokenKind.TkFloatLit Then
    Dim id As Integer = NewNode(NodeKind.NdFloatLit)
    g_nodes(id).intVal = g_tokens(p_pos).pos
    g_nodes(id).lenVal = g_tokens(p_pos).len
    PAdvance()
    Return id
  End If

  If k = TokenKind.TkTrue Then
    Dim id As Integer = NewNode(NodeKind.NdBoolLit)
    g_nodes(id).intVal = 1
    PAdvance()
    Return id
  End If

  If k = TokenKind.TkFalse Then
    Dim id As Integer = NewNode(NodeKind.NdBoolLit)
    g_nodes(id).intVal = 0
    PAdvance()
    Return id
  End If

  If k = TokenKind.TkStrLit Then
    Dim id As Integer = NewNode(NodeKind.NdStrLit)
    g_nodes(id).intVal = g_tokens(p_pos).pos
    g_nodes(id).lenVal = g_tokens(p_pos).len
    PAdvance()
    Return id
  End If

  If k = TokenKind.TkInterpStart Then
    PAdvance()
    Dim partBuf(64) As Integer
    Dim partCount As Integer = 0
    While (PPeekKind() <> TokenKind.TkInterpEnd) And (PPeekKind() <> TokenKind.TkEof) Do
      If PPeekKind() = TokenKind.TkStrLit Then
        Dim sId As Integer = NewNode(NodeKind.NdStrLit)
        g_nodes(sId).intVal = g_tokens(p_pos).pos
        g_nodes(sId).lenVal = g_tokens(p_pos).len
        PAdvance()
        If partCount < 64 Then
          partBuf(partCount) = sId
          partCount = partCount + 1
        End If
      Else
        If PPeekKind() = TokenKind.TkInterpExprStart Then
          PAdvance()
          PSkipNewlines()
          Dim eId As Integer = ParseExpr()
          PSkipNewlines()
          PExpect(TokenKind.TkInterpExprEnd)
          If partCount < 64 Then
            partBuf(partCount) = eId
            partCount = partCount + 1
          End If
        Else
          Printf(MSG_PARSE_UNEXPECTED_PRIMARY, PPeekKind(), g_tokens(p_pos).line)
          p_errors = p_errors + 1
          PAdvance()
        End If
      End If
    End While
    PExpect(TokenKind.TkInterpEnd)
    If partCount = 0 Then
      Dim emptyId As Integer = NewNode(NodeKind.NdStrLit)
      g_nodes(emptyId).intVal = 0
      g_nodes(emptyId).lenVal = 0
      Return emptyId
    End If
    If (partCount = 1) And (g_nodes(partBuf(0)).kind = NodeKind.NdStrLit) Then
      Return partBuf(0)
    End If
    Dim id As Integer = NewNode(NodeKind.NdStrInterp)
    g_nodes(id).firstChild = g_kidCount
    g_nodes(id).childCount = partCount
    Dim pi As Integer = 0
    While pi < partCount Do
      PushKid(partBuf(pi))
      pi = pi + 1
    End While
    Return id
  End If

  If k = TokenKind.TkLParen Then
    PAdvance()
    PSkipNewlines()
    If PPeekKind() = TokenKind.TkRParen Then
      PAdvance()
      Dim emptyLit As Integer = NewNode(NodeKind.NdArrayLit)
      Return emptyLit
    End If
    Dim first As Integer = ParseExpr()
    PSkipNewlines()
    If PPeekKind() = TokenKind.TkComma Then
      Dim elBuf(64) As Integer
      elBuf(0) = first
      Dim elCount As Integer = 1
      While PPeekKind() = TokenKind.TkComma Do
        PAdvance()
        PSkipNewlines()
        elBuf(elCount) = ParseExpr()
        elCount = elCount + 1
        PSkipNewlines()
      End While
      PExpect(TokenKind.TkRParen)
      Dim litId As Integer = NewNode(NodeKind.NdArrayLit)
      g_nodes(litId).firstChild = g_kidCount
      g_nodes(litId).childCount = elCount
      Dim i As Integer = 0
      While i < elCount Do
        PushKid(elBuf(i))
        i = i + 1
      End While
      Return litId
    End If
    PExpect(TokenKind.TkRParen)
    Return first
  End If

  If k = TokenKind.TkSizeOf Then
    PAdvance()
    PExpect(TokenKind.TkLParen)
    Dim tk, tik, tnPos, tnLen As Integer = 0
    PParseTypeSpec(ByRef tk, ByRef tik, ByRef tnPos, ByRef tnLen)
    PExpect(TokenKind.TkRParen)
    Dim id As Integer = NewNode(NodeKind.NdSizeOf)
    g_nodes(id).tyKind = tk
    g_nodes(id).tyInnerKind = tik
    g_nodes(id).tyNamePos = tnPos
    g_nodes(id).tyNameLen = tnLen
    Return id
  End If

  If k = TokenKind.TkArenaAlloc Then
    PAdvance()
    PExpect(TokenKind.TkLParen)
    Dim arenaArg As Integer = PParseCallArg()
    PExpect(TokenKind.TkComma)
    Dim tk, tik, tnPos, tnLen As Integer = 0
    PParseTypeSpec(ByRef tk, ByRef tik, ByRef tnPos, ByRef tnLen)
    Dim cntExpr As Integer = -1
    If PPeekKind() = TokenKind.TkComma Then
      PAdvance()
      cntExpr = ParseExpr()
    Else
      cntExpr = NewNode(NodeKind.NdIntLit)
      g_nodes(cntExpr).intVal = 1
    End If
    PExpect(TokenKind.TkRParen)
    Dim id As Integer = NewNode(NodeKind.NdArenaAlloc)
    g_nodes(id).op = g_nodes(arenaArg).op
    g_nodes(id).left = g_nodes(arenaArg).left
    g_nodes(id).right = cntExpr
    g_nodes(id).tyKind = tk
    g_nodes(id).tyInnerKind = tik
    g_nodes(id).tyNamePos = tnPos
    g_nodes(id).tyNameLen = tnLen
    Return id
  End If

  If k = TokenKind.TkHeapAlloc Then
    PAdvance()
    PExpect(TokenKind.TkLParen)
    Dim tk, tik, tnPos, tnLen As Integer = 0
    PParseTypeSpec(ByRef tk, ByRef tik, ByRef tnPos, ByRef tnLen)
    Dim cntExpr As Integer = -1
    If PPeekKind() = TokenKind.TkComma Then
      PAdvance()
      cntExpr = ParseExpr()
    Else
      cntExpr = NewNode(NodeKind.NdIntLit)
      g_nodes(cntExpr).intVal = 1
    End If
    PExpect(TokenKind.TkRParen)
    Dim id As Integer = NewNode(NodeKind.NdHeapAlloc)
    g_nodes(id).left = cntExpr
    g_nodes(id).tyKind = tk
    g_nodes(id).tyInnerKind = tik
    g_nodes(id).tyNamePos = tnPos
    g_nodes(id).tyNameLen = tnLen
    Return id
  End If

  If k = TokenKind.TkHeapRealloc Then
    PAdvance()
    PExpect(TokenKind.TkLParen)
    Dim ptrExpr As Integer = ParseExpr()
    PExpect(TokenKind.TkComma)
    Dim tk, tik, tnPos, tnLen As Integer = 0
    PParseTypeSpec(ByRef tk, ByRef tik, ByRef tnPos, ByRef tnLen)
    PExpect(TokenKind.TkComma)
    Dim cntExpr As Integer = ParseExpr()
    PExpect(TokenKind.TkRParen)
    Dim id As Integer = NewNode(NodeKind.NdHeapRealloc)
    g_nodes(id).left = ptrExpr
    g_nodes(id).right = cntExpr
    g_nodes(id).tyKind = tk
    g_nodes(id).tyInnerKind = tik
    g_nodes(id).tyNamePos = tnPos
    g_nodes(id).tyNameLen = tnLen
    Return id
  End If

  If k = TokenKind.TkVolatileLoad Then
    PAdvance()
    PExpect(TokenKind.TkLParen)
    Dim addrExpr As Integer = ParseExpr()
    PExpect(TokenKind.TkComma)
    Dim tk, tik, tnPos, tnLen As Integer = 0
    PParseTypeSpec(ByRef tk, ByRef tik, ByRef tnPos, ByRef tnLen)
    PExpect(TokenKind.TkRParen)
    Dim id As Integer = NewNode(NodeKind.NdVolatileLoad)
    g_nodes(id).left = addrExpr
    g_nodes(id).tyKind = tk
    g_nodes(id).tyInnerKind = tik
    g_nodes(id).tyNamePos = tnPos
    g_nodes(id).tyNameLen = tnLen
    Return id
  End If

  If k = TokenKind.TkVolatileStore Then
    PAdvance()
    PExpect(TokenKind.TkLParen)
    Dim addrExpr As Integer = ParseExpr()
    PExpect(TokenKind.TkComma)
    Dim tk, tik, tnPos, tnLen As Integer = 0
    PParseTypeSpec(ByRef tk, ByRef tik, ByRef tnPos, ByRef tnLen)
    PExpect(TokenKind.TkComma)
    Dim valExpr As Integer = ParseExpr()
    PExpect(TokenKind.TkRParen)
    Dim id As Integer = NewNode(NodeKind.NdVolatileStore)
    g_nodes(id).left = addrExpr
    g_nodes(id).right = valExpr
    g_nodes(id).tyKind = tk
    g_nodes(id).tyInnerKind = tik
    g_nodes(id).tyNamePos = tnPos
    g_nodes(id).tyNameLen = tnLen
    Return id
  End If

  If k = TokenKind.TkAddressOf Then
    PAdvance()
    PExpect(TokenKind.TkLParen)
    Dim targetExpr As Integer = ParseExpr()
    PExpect(TokenKind.TkRParen)
    Dim id As Integer = NewNode(NodeKind.NdAddressOf)
    g_nodes(id).left = targetExpr
    Return id
  End If

  If k = TokenKind.TkPtrRead Then
    PAdvance()
    PExpect(TokenKind.TkLParen)
    Dim addrExpr As Integer = ParseExpr()
    PExpect(TokenKind.TkComma)
    Dim tk, tik, tnPos, tnLen As Integer = 0
    PParseTypeSpec(ByRef tk, ByRef tik, ByRef tnPos, ByRef tnLen)
    PExpect(TokenKind.TkRParen)
    Dim id As Integer = NewNode(NodeKind.NdPtrRead)
    g_nodes(id).left = addrExpr
    g_nodes(id).tyKind = tk
    g_nodes(id).tyInnerKind = tik
    g_nodes(id).tyNamePos = tnPos
    g_nodes(id).tyNameLen = tnLen
    Return id
  End If

  If k = TokenKind.TkPtrWrite Then
    PAdvance()
    PExpect(TokenKind.TkLParen)
    Dim addrExpr As Integer = ParseExpr()
    PExpect(TokenKind.TkComma)
    Dim tk, tik, tnPos, tnLen As Integer = 0
    PParseTypeSpec(ByRef tk, ByRef tik, ByRef tnPos, ByRef tnLen)
    PExpect(TokenKind.TkComma)
    Dim valExpr As Integer = ParseExpr()
    PExpect(TokenKind.TkRParen)
    Dim id As Integer = NewNode(NodeKind.NdPtrWrite)
    g_nodes(id).left = addrExpr
    g_nodes(id).right = valExpr
    g_nodes(id).tyKind = tk
    g_nodes(id).tyInnerKind = tik
    g_nodes(id).tyNamePos = tnPos
    g_nodes(id).tyNameLen = tnLen
    Return id
  End If

  If k = TokenKind.TkPtrAdd Then
    PAdvance()
    PExpect(TokenKind.TkLParen)
    Dim ptrExpr As Integer = ParseExpr()
    PExpect(TokenKind.TkComma)
    Dim offExpr As Integer = ParseExpr()
    PExpect(TokenKind.TkRParen)
    Dim id As Integer = NewNode(NodeKind.NdPtrAdd)
    g_nodes(id).left = ptrExpr
    g_nodes(id).right = offExpr
    Return id
  End If

  If k = TokenKind.TkIdent Then
    Dim namePos As Integer = g_tokens(p_pos).pos
    Dim nameLen As Integer = g_tokens(p_pos).len
    PAdvance()
    If PPeekKind() = TokenKind.TkWith Then
      Return PParseWithInit(namePos, nameLen)
    End If
    If PPeekKind() = TokenKind.TkLParen Then
      PAdvance()
      Dim argBuf(32) As Integer
      Dim argCount As Integer = 0
      If PPeekKind() <> TokenKind.TkRParen Then
        Dim reading As Boolean = True
        While reading Do
          argBuf(argCount) = PParseCallArg()
          argCount = argCount + 1
          If PPeekKind() = TokenKind.TkComma Then
            PAdvance()
          Else
            reading = False
          End If
        End While
      End If
      PExpect(TokenKind.TkRParen)
      Dim callId As Integer = NewNode(NodeKind.NdCall)
      g_nodes(callId).intVal = namePos
      g_nodes(callId).lenVal = nameLen
      g_nodes(callId).firstChild = g_kidCount
      g_nodes(callId).childCount = argCount
      Dim i As Integer = 0
      While i < argCount Do
        PushKid(argBuf(i))
        i = i + 1
      End While
      Return callId
    Else
      Dim varId As Integer = NewNode(NodeKind.NdVar)
      g_nodes(varId).intVal = namePos
      g_nodes(varId).lenVal = nameLen
      Return varId
    End If
  End If

  Printf(MSG_PARSE_UNEXPECTED_PRIMARY, k, g_tokens(p_pos).line)
  p_errors = p_errors + 1
  PAdvance()
  Return NewNode(NodeKind.NdIntLit)
End Function

Private Function PParsePostfix() As Integer
  Dim cur As Integer = PParsePrimary()
  While PPeekKind() = TokenKind.TkDot Do
    PAdvance()
    Dim fPos As Integer = g_tokens(p_pos).pos
    Dim fLen As Integer = g_tokens(p_pos).len
    PExpect(TokenKind.TkIdent)
    Dim fa As Integer = NewNode(NodeKind.NdFieldAccess)
    g_nodes(fa).left = cur
    g_nodes(fa).intVal = fPos
    g_nodes(fa).lenVal = fLen
    cur = fa
    If PPeekKind() = TokenKind.TkLParen Then
      PAdvance()
      Dim idxE As Integer = ParseExpr()
      PExpect(TokenKind.TkRParen)
      Dim ia As Integer = NewNode(NodeKind.NdIndexAccess)
      g_nodes(ia).left = cur
      g_nodes(ia).right = idxE
      cur = ia
    End If
  End While
  Return cur
End Function

Private Function PParseUnary() As Integer
  If PPeekKind() = TokenKind.TkMinus Then
    PAdvance()
    Dim inner As Integer = PParseUnary()
    Dim id As Integer = NewNode(NodeKind.NdNeg)
    g_nodes(id).left = inner
    Return id
  End If
  If PPeekKind() = TokenKind.TkAwait Then
    PAdvance()
    Dim inner As Integer = PParseUnary()
    Dim id As Integer = NewNode(NodeKind.NdAwait)
    g_nodes(id).left = inner
    Return id
  End If
  Return PParsePostfix()
End Function

Private Function PParseMulDiv() As Integer
  Dim lhs As Integer = PParseUnary()
  Dim going As Boolean = True
  While going Do
    Dim k As Integer = PPeekKind()
    If (k = TokenKind.TkStar) Or (k = TokenKind.TkSlash) Or (k = TokenKind.TkMod) Then
      PAdvance()
      Dim rhs As Integer = PParseUnary()
      Dim bin As Integer = NewNode(NodeKind.NdBinOp)
      g_nodes(bin).op = k
      g_nodes(bin).left = lhs
      g_nodes(bin).right = rhs
      lhs = bin
    Else
      going = False
    End If
  End While
  Return lhs
End Function

Private Function PFoldStrLit(lhs, rhs As Integer) As Integer
  Dim p1 = g_nodes(lhs).intVal, l1 = g_nodes(lhs).lenVal, p2 = g_nodes(rhs).intVal, l2 = g_nodes(rhs).lenVal As Integer
  If (g_srcPoolLen + l1 + l2 + 1) >= g_srcPoolCap Then
    Dim bin As Integer = NewNode(NodeKind.NdBinOp)
    g_nodes(bin).op = TokenKind.TkAmp
    g_nodes(bin).left = lhs
    g_nodes(bin).right = rhs
    Return bin
  End If
  Dim startPos As Integer = g_srcPoolLen
  Dim i As Integer = 0
  While i < l1 Do
    g_srcPool(g_srcPoolLen) = g_srcPool(p1 + i)
    g_srcPoolLen = g_srcPoolLen + 1
    i = i + 1
  End While
  Dim j As Integer = 0
  While j < l2 Do
    g_srcPool(g_srcPoolLen) = g_srcPool(p2 + j)
    g_srcPoolLen = g_srcPoolLen + 1
    j = j + 1
  End While
  Dim id As Integer = NewNode(NodeKind.NdStrLit)
  g_nodes(id).intVal = startPos
  g_nodes(id).lenVal = l1 + l2
  Return id
End Function

Private Function PParseAddSub() As Integer
  Dim lhs As Integer = PParseMulDiv()
  Dim going As Boolean = True
  While going Do
    Dim k As Integer = PPeekKind()
    If (k = TokenKind.TkPlus) Or (k = TokenKind.TkMinus) Then
      PAdvance()
      Dim rhs As Integer = PParseMulDiv()
      If (k = TokenKind.TkPlus) And (g_nodes(lhs).kind = NodeKind.NdStrLit) And (g_nodes(rhs).kind = NodeKind.NdStrLit) Then
        lhs = PFoldStrLit(lhs, rhs)
      Else
        Dim bin As Integer = NewNode(NodeKind.NdBinOp)
        g_nodes(bin).op = k
        g_nodes(bin).left = lhs
        g_nodes(bin).right = rhs
        lhs = bin
      End If
    Else
      going = False
    End If
  End While
  Return lhs
End Function

Private Function PParseConcat() As Integer
  Dim lhs As Integer = PParseAddSub()
  While PPeekKind() = TokenKind.TkAmp Do
    PAdvance()
    PSkipNewlines()
    Dim rhs As Integer = PParseAddSub()
    If (g_nodes(lhs).kind = NodeKind.NdStrLit) And (g_nodes(rhs).kind = NodeKind.NdStrLit) Then
      lhs = PFoldStrLit(lhs, rhs)
    Else
      Dim bin As Integer = NewNode(NodeKind.NdBinOp)
      g_nodes(bin).op = TokenKind.TkAmp
      g_nodes(bin).left = lhs
      g_nodes(bin).right = rhs
      lhs = bin
    End If
  End While
  Return lhs
End Function

Private Function PParseShift() As Integer
  Dim lhs As Integer = PParseConcat()
  Dim going As Boolean = True
  While going Do
    Dim k As Integer = PPeekKind()
    If (k = TokenKind.TkShl) Or (k = TokenKind.TkShr) Then
      PAdvance()
      Dim rhs As Integer = PParseConcat()
      Dim bin As Integer = NewNode(NodeKind.NdBinOp)
      g_nodes(bin).op = k
      g_nodes(bin).left = lhs
      g_nodes(bin).right = rhs
      lhs = bin
    Else
      going = False
    End If
  End While
  Return lhs
End Function

Private Function PParseCmp() As Integer
  Dim lhs As Integer = PParseShift()
  Dim k As Integer = PPeekKind()
  If (k = TokenKind.TkEq) Or (k = TokenKind.TkNeq) Or (k = TokenKind.TkLt) Or (k = TokenKind.TkGt) Or (k = TokenKind.TkLe) Or (k = TokenKind.TkGe) Then
    PAdvance()
    Dim rhs As Integer = PParseShift()
    Dim bin As Integer = NewNode(NodeKind.NdBinOp)
    g_nodes(bin).op = k
    g_nodes(bin).left = lhs
    g_nodes(bin).right = rhs
    Return bin
  End If
  Return lhs
End Function

Private Function PParseNot() As Integer
  If PPeekKind() = TokenKind.TkNot Then
    PAdvance()
    Dim inner As Integer = PParseNot()
    Dim id As Integer = NewNode(NodeKind.NdNot)
    g_nodes(id).left = inner
    Return id
  End If
  Return PParseCmp()
End Function

Private Function PParseAnd() As Integer
  Dim lhs As Integer = PParseNot()
  While PPeekKind() = TokenKind.TkAnd Do
    PAdvance()
    Dim rhs As Integer = PParseNot()
    Dim bin As Integer = NewNode(NodeKind.NdBinOp)
    g_nodes(bin).op = TokenKind.TkAnd
    g_nodes(bin).left = lhs
    g_nodes(bin).right = rhs
    lhs = bin
  End While
  Return lhs
End Function

Public Function ParseExpr() As Integer
  Dim lhs As Integer = PParseAnd()
  Dim going As Boolean = True
  While going Do
    Dim k As Integer = PPeekKind()
    If (k = TokenKind.TkOr) Or (k = TokenKind.TkXor) Then
      PAdvance()
      Dim rhs As Integer = PParseAnd()
      Dim bin As Integer = NewNode(NodeKind.NdBinOp)
      g_nodes(bin).op = k
      g_nodes(bin).left = lhs
      g_nodes(bin).right = rhs
      lhs = bin
    Else
      going = False
    End If
  End While
  Return lhs
End Function

' Parses grouped same-type and/or comma-separated variable declarations:
'   a, b, c As Integer [= init]
'   a = 1, b = 2, c As Integer
'   x, y As Integer, flag As Boolean = True
'   reg As Integer At 0x40020014
Private Sub PParseVarDeclGroup(ndKind, vis, isVolatile, defModId As Integer, outBuf() As Integer, ByRef outCnt As Integer)
  Dim moreClauses As Boolean = True
  While moreClauses Do
    Dim nPos(32), nLen(32), aLen(32), iInit(32) As Integer
    Dim itemCnt = 0 As Integer, moreItems = True As Boolean
    While moreItems Do
      nPos(itemCnt) = g_tokens(p_pos).pos
      nLen(itemCnt) = g_tokens(p_pos).len
      PExpect(TokenKind.TkIdent)
      Dim arrLen As Integer = 0
      If PPeekKind() = TokenKind.TkLParen Then
        PAdvance()
        If PPeekKind() = TokenKind.TkRParen Then
          arrLen = -1
          PAdvance()
        Else
          arrLen = g_tokens(p_pos).intVal
          PExpect(TokenKind.TkIntLit)
          PExpect(TokenKind.TkRParen)
          If arrLen <= 0 Then
            arrLen = -2
          End If
        End If
      End If
      Dim itemInit As Integer = -1
      If PPeekKind() = TokenKind.TkEq Then
        PAdvance()
        If (arrLen <> 0) And (PPeekKind() = TokenKind.TkLParen) Then
          itemInit = PParseArrayLit()
        Else
          itemInit = ParseExpr()
        End If
        If (arrLen = -1) And (g_nodes(itemInit).kind = NodeKind.NdArrayLit) Then
          arrLen = g_nodes(itemInit).childCount
        End If
      End If
      aLen(itemCnt) = arrLen
      iInit(itemCnt) = itemInit
      itemCnt = itemCnt + 1
      If PPeekKind() = TokenKind.TkComma Then
        PAdvance()
        PSkipNewlines()
      Else
        moreItems = False
      End If
    End While

    PExpect(TokenKind.TkAs)
    Dim tk As Integer = 0
    Dim tik As Integer = 0
    Dim tnPos As Integer = 0
    Dim tnLen As Integer = 0
    PParseTypeSpec(ByRef tk, ByRef tik, ByRef tnPos, ByRef tnLen)

    Dim atExpr As Integer = -1
    If PPeekKind() = TokenKind.TkIdent Then
      If SliceEqualsStr(g_tokens(p_pos).pos, g_tokens(p_pos).len, "At", 2) Then
        PAdvance()
        atExpr = PParseUnary()
      End If
    End If

    Dim sharedInit As Integer = -1
    Dim firstArrLen As Integer = 0
    If itemCnt > 0 Then
      firstArrLen = aLen(0)
    End If
    If PPeekKind() = TokenKind.TkWith Then
      sharedInit = PParseWithInit(tnPos, tnLen)
    Else
      If (firstArrLen = 0) And (tk = TypeKind.TyStruct) And (PPeekKind() = TokenKind.TkLParen) Then
        PAdvance()
        Dim argBuf(32) As Integer
        Dim argCount As Integer = 0
        If PPeekKind() <> TokenKind.TkRParen Then
          Dim reading As Boolean = True
          While reading Do
            argBuf(argCount) = PParseCallArg()
            argCount = argCount + 1
            If PPeekKind() = TokenKind.TkComma Then
              PAdvance()
            Else
              reading = False
            End If
          End While
        End If
        PExpect(TokenKind.TkRParen)
        Dim callId As Integer = NewNode(NodeKind.NdCall)
        g_nodes(callId).intVal = tnPos
        g_nodes(callId).lenVal = tnLen
        g_nodes(callId).firstChild = g_kidCount
        g_nodes(callId).childCount = argCount
        Dim ci As Integer = 0
        While ci < argCount Do
          PushKid(argBuf(ci))
          ci = ci + 1
        End While
        sharedInit = callId
      Else
        If PPeekKind() = TokenKind.TkEq Then
          PAdvance()
          If (firstArrLen <> 0) And (PPeekKind() = TokenKind.TkLParen) Then
            sharedInit = PParseArrayLit()
          Else
            sharedInit = ParseExpr()
          End If
        End If
      End If
    End If

    Dim vi As Integer = 0
    While vi < itemCnt Do
      Dim curArrLen As Integer = aLen(vi)
      Dim curInit As Integer = iInit(vi)
      If curInit < 0 Then
        curInit = sharedInit
      End If
      If (curArrLen = -1) And (curInit >= 0) And (g_nodes(curInit).kind = NodeKind.NdArrayLit) Then
        curArrLen = g_nodes(curInit).childCount
      End If
      Dim id As Integer = NewNode(ndKind)
      If ndKind = NodeKind.NdGlobalVar Then
        g_nodes(id).op = vis
        g_nodes(id).modId = defModId
      End If
      g_nodes(id).intVal = nPos(vi)
      g_nodes(id).lenVal = nLen(vi)
      g_nodes(id).left = curInit
      g_nodes(id).right = atExpr
      g_nodes(id).flags = isVolatile
      g_nodes(id).tyKind = tk
      g_nodes(id).tyInnerKind = tik
      g_nodes(id).tyArrLen = curArrLen
      g_nodes(id).tyNamePos = tnPos
      g_nodes(id).tyNameLen = tnLen
      outBuf(outCnt) = id
      outCnt = outCnt + 1
      vi = vi + 1
    End While

    If PPeekKind() = TokenKind.TkComma Then
      PAdvance()
      PSkipNewlines()
    Else
      moreClauses = False
    End If
  End While
End Sub

' stopMode: 0 = stop at End/Eof, 1 = stop at Else/End/Eof, 2 = stop at Case/End/Eof
Private Sub PParseBlock(stopMode As Integer, ByRef outStart, outCount As Integer)
  Dim buf(512) As Integer
  Dim cnt As Integer = 0
  Dim running As Boolean = True
  While running Do
    PSkipNewlines()
    Dim k As Integer = PPeekKind()
    If (k = TokenKind.TkEnd) Or (k = TokenKind.TkEof) Then
      running = False
    Else
      If (stopMode = 1) And (k = TokenKind.TkElse) Then
        running = False
      Else
        If (stopMode = 2) And (k = TokenKind.TkCase) Then
          running = False
        Else
          PParseStmtInto(buf, ByRef cnt)
        End If
      End If
    End If
  End While
  outStart = g_kidCount
  outCount = cnt
  Dim i As Integer = 0
  While i < cnt Do
    PushKid(buf(i))
    i = i + 1
  End While
End Sub

Private Sub PParseStmtInto(outBuf() As Integer, ByRef outCnt As Integer)
  Dim k As Integer = PPeekKind()

  ' [Volatile] Dim / Static name[(N | )] [= init], ... As Type [At addr] [= expr | With ( ... ) | (args)], ...
  Dim isVol As Integer = 0
  If k = TokenKind.TkVolatile Then
    isVol = 1
    PAdvance()
    k = PPeekKind()
  End If
  If (k = TokenKind.TkDim) Or (k = TokenKind.TkStatic) Then
    Dim ndKind As Integer = NodeKind.NdDim
    If k = TokenKind.TkStatic Then
      ndKind = NodeKind.NdStaticDim
    End If
    PAdvance()
    PParseVarDeclGroup(ndKind, 0, isVol, 0, outBuf, ByRef outCnt)
    Return
  End If

  outBuf(outCnt) = ParseStmt()
  outCnt = outCnt + 1
End Sub

Public Function ParseStmt() As Integer
  Dim k As Integer = PPeekKind()

  ' Return [expr]
  If k = TokenKind.TkReturn Then
    PAdvance()
    Dim id As Integer = NewNode(NodeKind.NdReturn)
    Dim nk As Integer = PPeekKind()
    If (nk <> TokenKind.TkNewline) And (nk <> TokenKind.TkEof) And (nk <> TokenKind.TkEnd) Then
      g_nodes(id).left = ParseExpr()
    End If
    Return id
  End If

  ' Label name
  If k = TokenKind.TkLabel Then
    PAdvance()
    Dim lPos As Integer = g_tokens(p_pos).pos
    Dim lLen As Integer = g_tokens(p_pos).len
    PExpect(TokenKind.TkIdent)
    Dim id As Integer = NewNode(NodeKind.NdLabel)
    g_nodes(id).intVal = lPos
    g_nodes(id).lenVal = lLen
    Return id
  End If

  ' GoTo name
  If k = TokenKind.TkGoTo Then
    PAdvance()
    Dim lPos As Integer = g_tokens(p_pos).pos
    Dim lLen As Integer = g_tokens(p_pos).len
    PExpect(TokenKind.TkIdent)
    Dim id As Integer = NewNode(NodeKind.NdGoTo)
    g_nodes(id).intVal = lPos
    g_nodes(id).lenVal = lLen
    Return id
  End If

  ' Await expr (statement form for Async Sub / Task)
  If k = TokenKind.TkAwait Then
    Dim awExpr As Integer = PParseUnary()
    Dim stmtId As Integer = NewNode(NodeKind.NdExprStmt)
    g_nodes(stmtId).left = awExpr
    Return stmtId
  End If

  ' Assert cond [, msg]
  If k = TokenKind.TkAssert Then
    Dim lineNum As Integer = g_tokens(p_pos).line
    PAdvance()
    Dim condId As Integer = ParseExpr()
    Dim msgId As Integer = -1
    If PPeekKind() = TokenKind.TkComma Then
      PAdvance()
      msgId = ParseExpr()
    End If
    Dim id As Integer = NewNode(NodeKind.NdAssert)
    g_nodes(id).intVal = lineNum
    g_nodes(id).left = condId
    g_nodes(id).right = msgId
    Return id
  End If

  ' If cond Then ... [Else ...] End If
  If k = TokenKind.TkIf Then
    PAdvance()
    Dim condId As Integer = ParseExpr()
    PExpect(TokenKind.TkThen)
    Dim thenStart As Integer = 0
    Dim thenCount As Integer = 0
    PParseBlock(1, ByRef thenStart, ByRef thenCount)
    Dim elseStart As Integer = 0
    Dim elseCount As Integer = 0
    If PPeekKind() = TokenKind.TkElse Then
      PAdvance()
      PParseBlock(0, ByRef elseStart, ByRef elseCount)
    End If
    PExpect(TokenKind.TkEnd)
    PExpect(TokenKind.TkIf)
    Dim id As Integer = NewNode(NodeKind.NdIf)
    g_nodes(id).left = condId
    g_nodes(id).firstChild = thenStart
    g_nodes(id).childCount = thenCount
    g_nodes(id).extraStart = elseStart
    g_nodes(id).extraCount = elseCount
    Return id
  End If

  ' While cond Do ... End While
  If k = TokenKind.TkWhile Then
    PAdvance()
    Dim condId As Integer = ParseExpr()
    PExpect(TokenKind.TkDo)
    Dim bodyStart As Integer = 0
    Dim bodyCount As Integer = 0
    PParseBlock(0, ByRef bodyStart, ByRef bodyCount)
    PExpect(TokenKind.TkEnd)
    PExpect(TokenKind.TkWhile)
    Dim id As Integer = NewNode(NodeKind.NdWhile)
    g_nodes(id).left = condId
    g_nodes(id).firstChild = bodyStart
    g_nodes(id).childCount = bodyCount
    Return id
  End If

  ' For var = start To finish ... End For
  If k = TokenKind.TkFor Then
    PAdvance()
    Dim varPos As Integer = g_tokens(p_pos).pos
    Dim varLen As Integer = g_tokens(p_pos).len
    PExpect(TokenKind.TkIdent)
    PExpect(TokenKind.TkEq)
    Dim startE As Integer = ParseExpr()
    PExpect(TokenKind.TkTo)
    Dim finishE As Integer = ParseExpr()
    Dim bodyStart As Integer = 0
    Dim bodyCount As Integer = 0
    PParseBlock(0, ByRef bodyStart, ByRef bodyCount)
    PExpect(TokenKind.TkEnd)
    PExpect(TokenKind.TkFor)
    Dim id As Integer = NewNode(NodeKind.NdFor)
    g_nodes(id).intVal = varPos
    g_nodes(id).lenVal = varLen
    g_nodes(id).left = startE
    g_nodes(id).right = finishE
    g_nodes(id).firstChild = bodyStart
    g_nodes(id).childCount = bodyCount
    Return id
  End If

  ' Select Case target ... Case e1, e2 ... [Case Else ...] End Select
  If k = TokenKind.TkSelect Then
    PAdvance()
    PExpect(TokenKind.TkCase)
    Dim targetE As Integer = ParseExpr()
    PSkipNewlines()
    Dim brBuf(64) As Integer
    Dim brCount As Integer = 0
    Dim defStart As Integer = 0
    Dim defCount As Integer = 0
    Dim hasDef As Integer = 0
    While PPeekKind() = TokenKind.TkCase Do
      PAdvance()
      If PPeekKind() = TokenKind.TkElse Then
        PAdvance()
        hasDef = 1
        PParseBlock(0, ByRef defStart, ByRef defCount)
      Else
        Dim valBuf(32) As Integer
        Dim valCount As Integer = 0
        Dim reading As Boolean = True
        While reading Do
          valBuf(valCount) = ParseExpr()
          valCount = valCount + 1
          If PPeekKind() = TokenKind.TkComma Then
            PAdvance()
          Else
            reading = False
          End If
        End While
        Dim bStart As Integer = 0
        Dim bCount As Integer = 0
        PParseBlock(2, ByRef bStart, ByRef bCount)
        Dim brId As Integer = NewNode(NodeKind.NdCaseBranch)
        g_nodes(brId).firstChild = bStart
        g_nodes(brId).childCount = bCount
        g_nodes(brId).extraStart = g_kidCount
        g_nodes(brId).extraCount = valCount
        Dim vi As Integer = 0
        While vi < valCount Do
          PushKid(valBuf(vi))
          vi = vi + 1
        End While
        brBuf(brCount) = brId
        brCount = brCount + 1
      End If
      PSkipNewlines()
    End While
    PExpect(TokenKind.TkEnd)
    PExpect(TokenKind.TkSelect)
    Dim selId As Integer = NewNode(NodeKind.NdSelect)
    g_nodes(selId).op = hasDef
    g_nodes(selId).left = targetE
    g_nodes(selId).firstChild = g_kidCount
    g_nodes(selId).childCount = brCount
    g_nodes(selId).extraStart = defStart
    g_nodes(selId).extraCount = defCount
    Dim bi As Integer = 0
    While bi < brCount Do
      PushKid(brBuf(bi))
      bi = bi + 1
    End While
    Return selId
  End If

  ' Postfix expression or Assignment (id = e, a.b = e, arr(i) = e, r.data(i) = e)
  Dim lhs As Integer = PParsePostfix()
  If PPeekKind() = TokenKind.TkEq Then
    PAdvance()
    Dim rhs As Integer = ParseExpr()
    Dim lk As Integer = g_nodes(lhs).kind
    If lk = NodeKind.NdVar Then
      Dim id As Integer = NewNode(NodeKind.NdAssign)
      g_nodes(id).intVal = g_nodes(lhs).intVal
      g_nodes(id).lenVal = g_nodes(lhs).lenVal
      g_nodes(id).left = rhs
      Return id
    End If
    If lk = NodeKind.NdFieldAccess Then
      Dim id As Integer = NewNode(NodeKind.NdFieldAssign)
      g_nodes(id).left = g_nodes(lhs).left
      g_nodes(id).intVal = g_nodes(lhs).intVal
      g_nodes(id).lenVal = g_nodes(lhs).lenVal
      g_nodes(id).right = rhs
      Return id
    End If
    If lk = NodeKind.NdIndexAccess Then
      Dim id As Integer = NewNode(NodeKind.NdIndexAssign)
      g_nodes(id).left = g_nodes(lhs).left
      g_nodes(id).right = g_nodes(lhs).right
      g_nodes(id).third = rhs
      Return id
    End If
    If (lk = NodeKind.NdCall) And (g_nodes(lhs).childCount = 1) Then
      Dim baseVar As Integer = NewNode(NodeKind.NdVar)
      g_nodes(baseVar).intVal = g_nodes(lhs).intVal
      g_nodes(baseVar).lenVal = g_nodes(lhs).lenVal
      Dim argNode As Integer = g_kids(g_nodes(lhs).firstChild)
      Dim id As Integer = NewNode(NodeKind.NdIndexAssign)
      g_nodes(id).left = baseVar
      g_nodes(id).right = g_nodes(argNode).left
      g_nodes(id).third = rhs
      Return id
    End If
    Printf(MSG_PARSE_INVALID_ASSIGN, g_tokens(p_pos).line)
    p_errors = p_errors + 1
  End If

  Dim stmtId As Integer = NewNode(NodeKind.NdExprStmt)
  g_nodes(stmtId).left = lhs
  Return stmtId
End Function

' Parses parameter list with optional same-type grouping:
'   (a, b As Integer, ByRef x, y As Single, ...)
Private Sub PParseParamList(allowEllipsis As Integer, paramBuf() As Integer, ByRef paramCount, outIsVar As Integer)
  paramCount = 0
  outIsVar = 0
  PSkipNewlines()
  If PPeekKind() = TokenKind.TkRParen Then
    Return
  End If
  Dim grpStart As Integer = 0
  Dim grpByRef As Integer = 0
  Dim reading As Boolean = True
  While reading Do
    PSkipNewlines()
    If (allowEllipsis = 1) And (PPeekKind() = TokenKind.TkEllipsis) Then
      If paramCount > grpStart Then
        PExpect(TokenKind.TkAs)
      End If
      outIsVar = 1
      PAdvance()
      reading = False
    Else
      Dim isByRef As Integer = grpByRef
      If PPeekKind() = TokenKind.TkByRef Then
        isByRef = 1
        grpByRef = 1
        PAdvance()
      Else
        If PPeekKind() = TokenKind.TkByVal Then
          isByRef = 0
          grpByRef = 0
          PAdvance()
        End If
      End If
      Dim pPos As Integer = g_tokens(p_pos).pos
      Dim pLen As Integer = g_tokens(p_pos).len
      PExpect(TokenKind.TkIdent)
      Dim arrLen As Integer = 0
      If PPeekKind() = TokenKind.TkLParen Then
        PAdvance()
        If PPeekKind() = TokenKind.TkRParen Then
          arrLen = -1
          PAdvance()
        Else
          arrLen = g_tokens(p_pos).intVal
          PExpect(TokenKind.TkIntLit)
          PExpect(TokenKind.TkRParen)
        End If
      End If
      Dim pNode As Integer = NewNode(NodeKind.NdParam)
      g_nodes(pNode).op = isByRef
      g_nodes(pNode).intVal = pPos
      g_nodes(pNode).lenVal = pLen
      g_nodes(pNode).tyArrLen = arrLen
      paramBuf(paramCount) = pNode
      paramCount = paramCount + 1

      If PPeekKind() = TokenKind.TkAs Then
        PAdvance()
        Dim tk As Integer = 0
        Dim tik As Integer = 0
        Dim tnPos As Integer = 0
        Dim tnLen As Integer = 0
        PParseTypeSpec(ByRef tk, ByRef tik, ByRef tnPos, ByRef tnLen)
        Dim gi As Integer = grpStart
        While gi < paramCount Do
          Dim pn As Integer = paramBuf(gi)
          g_nodes(pn).tyKind = tk
          g_nodes(pn).tyInnerKind = tik
          g_nodes(pn).tyNamePos = tnPos
          g_nodes(pn).tyNameLen = tnLen
          gi = gi + 1
        End While
        grpStart = paramCount
        grpByRef = 0
      End If

      PSkipNewlines()
      If PPeekKind() = TokenKind.TkComma Then
        PAdvance()
        PSkipNewlines()
      Else
        reading = False
      End If
    End If
  End While
  If paramCount > grpStart Then
    PExpect(TokenKind.TkAs)
  End If
End Sub

Private Sub PParseDefInto(defBuf() As Integer, ByRef defCount As Integer)
  Dim defModId As Integer = g_tokens(p_pos).modId
  Dim vis As Integer = 0
  If PPeekKind() = TokenKind.TkPublic Then
    vis = 1
    PAdvance()
  Else
    If PPeekKind() = TokenKind.TkPrivate Then
      vis = 2
      PAdvance()
    End If
  End If

  Dim k As Integer = PPeekKind()

  ' Structure Name ... End Structure (supports grouped Dim a, b As T, c As U)
  If k = TokenKind.TkStructure Then
    PAdvance()
    Dim sPos As Integer = g_tokens(p_pos).pos
    Dim sLen As Integer = g_tokens(p_pos).len
    PExpect(TokenKind.TkIdent)
    PSkipNewlines()
    Dim fBuf(128) As Integer
    Dim fCount As Integer = 0
    While (PPeekKind() <> TokenKind.TkEnd) And (PPeekKind() <> TokenKind.TkEof) Do
      PExpect(TokenKind.TkDim)
      Dim moreClauses As Boolean = True
      While moreClauses Do
        Dim fnPos(32) As Integer
        Dim fnLen(32) As Integer
        Dim faLen(32) As Integer
        Dim itemCnt As Integer = 0
        Dim moreItems As Boolean = True
        While moreItems Do
          fnPos(itemCnt) = g_tokens(p_pos).pos
          fnLen(itemCnt) = g_tokens(p_pos).len
          PExpect(TokenKind.TkIdent)
          Dim arrLen As Integer = 0
          If PPeekKind() = TokenKind.TkLParen Then
            PAdvance()
            arrLen = g_tokens(p_pos).intVal
            PExpect(TokenKind.TkIntLit)
            PExpect(TokenKind.TkRParen)
            If arrLen <= 0 Then
              arrLen = -2
            End If
          End If
          faLen(itemCnt) = arrLen
          itemCnt = itemCnt + 1
          If PPeekKind() = TokenKind.TkComma Then
            PAdvance()
            PSkipNewlines()
          Else
            moreItems = False
          End If
        End While
        PExpect(TokenKind.TkAs)
        Dim tk As Integer = 0
        Dim tik As Integer = 0
        Dim tnPos As Integer = 0
        Dim tnLen As Integer = 0
        PParseTypeSpec(ByRef tk, ByRef tik, ByRef tnPos, ByRef tnLen)
        Dim fi As Integer = 0
        While fi < itemCnt Do
          Dim fd As Integer = NewNode(NodeKind.NdFieldDecl)
          g_nodes(fd).intVal = fnPos(fi)
          g_nodes(fd).lenVal = fnLen(fi)
          g_nodes(fd).tyKind = tk
          g_nodes(fd).tyInnerKind = tik
          g_nodes(fd).tyArrLen = faLen(fi)
          g_nodes(fd).tyNamePos = tnPos
          g_nodes(fd).tyNameLen = tnLen
          fBuf(fCount) = fd
          fCount = fCount + 1
          fi = fi + 1
        End While
        If PPeekKind() = TokenKind.TkComma Then
          PAdvance()
          PSkipNewlines()
        Else
          moreClauses = False
        End If
      End While
      PSkipNewlines()
    End While
    PExpect(TokenKind.TkEnd)
    PExpect(TokenKind.TkStructure)
    Dim sd As Integer = NewNode(NodeKind.NdStructDef)
    g_nodes(sd).op = vis
    g_nodes(sd).modId = defModId
    g_nodes(sd).intVal = sPos
    g_nodes(sd).lenVal = sLen
    g_nodes(sd).firstChild = g_kidCount
    g_nodes(sd).childCount = fCount
    Dim i As Integer = 0
    While i < fCount Do
      PushKid(fBuf(i))
      i = i + 1
    End While
    defBuf(defCount) = sd
    defCount = defCount + 1
    Return
  End If

  ' Enum Name ... Member = Val ... End Enum
  If k = TokenKind.TkEnum Then
    PAdvance()
    Dim ePos As Integer = g_tokens(p_pos).pos
    Dim eLen As Integer = g_tokens(p_pos).len
    PExpect(TokenKind.TkIdent)
    PSkipNewlines()
    Dim mBuf(128) As Integer
    Dim mCount As Integer = 0
    While (PPeekKind() <> TokenKind.TkEnd) And (PPeekKind() <> TokenKind.TkEof) Do
      Dim mPos As Integer = g_tokens(p_pos).pos
      Dim mLen As Integer = g_tokens(p_pos).len
      PExpect(TokenKind.TkIdent)
      PExpect(TokenKind.TkEq)
      Dim sign As Integer = 1
      If PPeekKind() = TokenKind.TkMinus Then
        sign = -1
        PAdvance()
      End If
      Dim mVal As Integer = sign * g_tokens(p_pos).intVal
      PExpect(TokenKind.TkIntLit)
      PSkipNewlines()
      Dim md As Integer = NewNode(NodeKind.NdEnumMember)
      g_nodes(md).intVal = mPos
      g_nodes(md).lenVal = mLen
      g_nodes(md).op = mVal
      mBuf(mCount) = md
      mCount = mCount + 1
    End While
    PExpect(TokenKind.TkEnd)
    PExpect(TokenKind.TkEnum)
    Dim ed As Integer = NewNode(NodeKind.NdEnumDef)
    g_nodes(ed).op = vis
    g_nodes(ed).modId = defModId
    g_nodes(ed).intVal = ePos
    g_nodes(ed).lenVal = eLen
    g_nodes(ed).firstChild = g_kidCount
    g_nodes(ed).childCount = mCount
    Dim i As Integer = 0
    While i < mCount Do
      PushKid(mBuf(i))
      i = i + 1
    End While
    defBuf(defCount) = ed
    defCount = defCount + 1
    Return
  End If

  ' [Public | Private] [Volatile] Static name[(N | )] [= init], ... As T [At addr] [= expr | With ( ... )], ...
  Dim isVol As Integer = 0
  If k = TokenKind.TkVolatile Then
    isVol = 1
    PAdvance()
    k = PPeekKind()
  End If
  If k = TokenKind.TkStatic Then
    PAdvance()
    PParseVarDeclGroup(NodeKind.NdGlobalVar, vis, isVol, defModId, defBuf, ByRef defCount)
    Return
  End If

  ' Declare Function/Sub name(params [, ...]) [As T]
  If k = TokenKind.TkDeclare Then
    PAdvance()
    Dim isSub As Integer = 0
    If PPeekKind() = TokenKind.TkSub Then
      isSub = 1
      PAdvance()
    Else
      PExpect(TokenKind.TkFunction)
    End If
    Dim nPos As Integer = g_tokens(p_pos).pos
    Dim nLen As Integer = g_tokens(p_pos).len
    PExpect(TokenKind.TkIdent)
    PExpect(TokenKind.TkLParen)
    Dim paramBuf(32) As Integer
    Dim paramCount As Integer = 0
    Dim isVar As Integer = 0
    PParseParamList(1, paramBuf, ByRef paramCount, ByRef isVar)
    PExpect(TokenKind.TkRParen)
    Dim rKind As Integer = TypeKind.TyNothing
    Dim rInnerKind As Integer = TypeKind.TyNothing
    Dim rnPos As Integer = 0
    Dim rnLen As Integer = 0
    If isSub = 0 Then
      PExpect(TokenKind.TkAs)
      PParseTypeSpec(ByRef rKind, ByRef rInnerKind, ByRef rnPos, ByRef rnLen)
    End If
    Dim ex As Integer = NewNode(NodeKind.NdExternDecl)
    g_nodes(ex).op = isVar
    g_nodes(ex).intVal = nPos
    g_nodes(ex).lenVal = nLen
    g_nodes(ex).tyKind = rKind
    g_nodes(ex).tyInnerKind = rInnerKind
    g_nodes(ex).tyNamePos = rnPos
    g_nodes(ex).tyNameLen = rnLen
    g_nodes(ex).extraStart = g_kidCount
    g_nodes(ex).extraCount = paramCount
    Dim i As Integer = 0
    While i < paramCount Do
      PushKid(paramBuf(i))
      i = i + 1
    End While
    defBuf(defCount) = ex
    defCount = defCount + 1
    Return
  End If

  ' Test "name" ... End Test
  If k = TokenKind.TkTest Then
    PAdvance()
    Dim tPos As Integer = g_tokens(p_pos).pos
    Dim tLen As Integer = g_tokens(p_pos).len
    PExpect(TokenKind.TkStrLit)
    Dim bodyStart As Integer = 0
    Dim bodyCount As Integer = 0
    PParseBlock(0, ByRef bodyStart, ByRef bodyCount)
    PExpect(TokenKind.TkEnd)
    PExpect(TokenKind.TkTest)
    Dim td As Integer = NewNode(NodeKind.NdTestDef)
    g_nodes(td).modId = defModId
    g_nodes(td).intVal = tPos
    g_nodes(td).lenVal = tLen
    g_nodes(td).firstChild = bodyStart
    g_nodes(td).childCount = bodyCount
    defBuf(defCount) = td
    defCount = defCount + 1
    Return
  End If

  ' [Async] Function / Sub definition
  Dim isAsync As Integer = 0
  If k = TokenKind.TkAsync Then
    isAsync = 1
    PAdvance()
    k = PPeekKind()
  End If
  Dim isSub As Integer = 0
  If k = TokenKind.TkSub Then
    isSub = 1
    PAdvance()
  Else
    PExpect(TokenKind.TkFunction)
  End If
  Dim namePos As Integer = g_tokens(p_pos).pos
  Dim nameLen As Integer = g_tokens(p_pos).len
  PExpect(TokenKind.TkIdent)
  PExpect(TokenKind.TkLParen)
  Dim paramBuf(32) As Integer
  Dim paramCount As Integer = 0
  Dim dummyVar As Integer = 0
  PParseParamList(0, paramBuf, ByRef paramCount, ByRef dummyVar)
  PExpect(TokenKind.TkRParen)

  Dim retKind As Integer = TypeKind.TyNothing
  Dim retInnerKind As Integer = TypeKind.TyNothing
  Dim retNamePos As Integer = 0
  Dim retNameLen As Integer = 0
  If isSub = 0 Then
    PExpect(TokenKind.TkAs)
    PParseTypeSpec(ByRef retKind, ByRef retInnerKind, ByRef retNamePos, ByRef retNameLen)
  End If

  Dim bodyStart As Integer = 0
  Dim bodyCount As Integer = 0
  PParseBlock(0, ByRef bodyStart, ByRef bodyCount)

  PExpect(TokenKind.TkEnd)
  If isSub = 1 Then
    PExpect(TokenKind.TkSub)
  Else
    PExpect(TokenKind.TkFunction)
  End If

  Dim fnId As Integer = NewNode(NodeKind.NdFuncDef)
  g_nodes(fnId).op = vis
  g_nodes(fnId).flags = isAsync
  g_nodes(fnId).modId = defModId
  g_nodes(fnId).intVal = namePos
  g_nodes(fnId).lenVal = nameLen
  g_nodes(fnId).tyKind = retKind
  g_nodes(fnId).tyInnerKind = retInnerKind
  g_nodes(fnId).tyNamePos = retNamePos
  g_nodes(fnId).tyNameLen = retNameLen
  g_nodes(fnId).firstChild = bodyStart
  g_nodes(fnId).childCount = bodyCount
  g_nodes(fnId).extraStart = g_kidCount
  g_nodes(fnId).extraCount = paramCount
  Dim i As Integer = 0
  While i < paramCount Do
    PushKid(paramBuf(i))
    i = i + 1
  End While
  defBuf(defCount) = fnId
  defCount = defCount + 1
End Sub

Public Function ParseProgram() As Integer
  p_pos = 0
  p_errors = 0
  Dim defBuf(1024) As Integer
  Dim defCount As Integer = 0
  PSkipNewlines()
  While PPeekKind() <> TokenKind.TkEof Do
    PParseDefInto(defBuf, ByRef defCount)
    PSkipNewlines()
  End While
  Dim progId As Integer = NewNode(NodeKind.NdProgram)
  g_nodes(progId).op = p_errors
  g_nodes(progId).firstChild = g_kidCount
  g_nodes(progId).childCount = defCount
  Dim i As Integer = 0
  While i < defCount Do
    PushKid(defBuf(i))
    i = i + 1
  End While
  Return progId
End Function
