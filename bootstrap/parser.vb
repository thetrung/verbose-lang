' VerBose Bootstrap Compiler (vbc) - Recursive-Descent Parser

Include "lexer.vb"
Include "ast.vb"

Private Static p_pos As Integer = 0

Private Function PeekKind() As Integer
  If p_pos >= g_tokCount Then
    Return TokenKind.TkEof
  End If
  Return g_tokens(p_pos).kind
End Function

Private Function PeekNextKind() As Integer
  If (p_pos + 1) >= g_tokCount Then
    Return TokenKind.TkEof
  End If
  Return g_tokens(p_pos + 1).kind
End Function

Private Sub Advance()
  If p_pos < g_tokCount Then
    p_pos = p_pos + 1
  End If
End Sub

Private Sub SkipNewlines()
  While PeekKind() = TokenKind.TkNewline Do
    Advance()
  End While
End Sub

Private Sub Expect(k As Integer)
  If PeekKind() <> k Then
    printf("vbc parse error: expected token %d, got %d at line %d\n", k, PeekKind(), g_tokens(p_pos).line)
  End If
  Advance()
End Sub

Private Function ParsePrimary() As Integer
  Dim k As Integer = PeekKind()
  If k = TokenKind.TkIntLit Then
    Dim id As Integer = NewNode(NodeKind.NdIntLit)
    g_nodes(id).intVal = g_tokens(p_pos).intVal
    Advance()
    Return id
  End If

  If k = TokenKind.TkStrLit Then
    Dim id As Integer = NewNode(NodeKind.NdStrLit)
    g_nodes(id).intVal = g_tokens(p_pos).pos
    g_nodes(id).lenVal = g_tokens(p_pos).len
    Advance()
    Return id
  End If

  If k = TokenKind.TkLParen Then
    Advance()
    Dim inner As Integer = ParseExpr()
    Expect(TokenKind.TkRParen)
    Return inner
  End If

  If k = TokenKind.TkIdent Then
    Dim namePos As Integer = g_tokens(p_pos).pos
    Dim nameLen As Integer = g_tokens(p_pos).len
    Advance()
    If PeekKind() = TokenKind.TkLParen Then
      Advance()
      Dim argBuf(32) As Integer
      Dim argCount As Integer = 0
      If PeekKind() <> TokenKind.TkRParen Then
        Dim reading As Boolean = True
        While reading Do
          argBuf(argCount) = ParseExpr()
          argCount = argCount + 1
          If PeekKind() = TokenKind.TkComma Then
            Advance()
          Else
            reading = False
          End If
        End While
      End If
      Expect(TokenKind.TkRParen)
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

  printf("vbc parse error: unexpected token %d in primary at line %d\n", k, g_tokens(p_pos).line)
  Advance()
  Return NewNode(NodeKind.NdIntLit)
End Function

Private Function ParseUnary() As Integer
  If PeekKind() = TokenKind.TkMinus Then
    Advance()
    Dim inner As Integer = ParseUnary()
    Dim id As Integer = NewNode(NodeKind.NdNeg)
    g_nodes(id).left = inner
    Return id
  End If
  Return ParsePrimary()
End Function

Private Function ParseMulDiv() As Integer
  Dim lhs As Integer = ParseUnary()
  Dim going As Boolean = True
  While going Do
    Dim k As Integer = PeekKind()
    If (k = TokenKind.TkStar) Or (k = TokenKind.TkSlash) Then
      Advance()
      Dim rhs As Integer = ParseUnary()
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

Private Function ParseAddSub() As Integer
  Dim lhs As Integer = ParseMulDiv()
  Dim going As Boolean = True
  While going Do
    Dim k As Integer = PeekKind()
    If (k = TokenKind.TkPlus) Or (k = TokenKind.TkMinus) Then
      Advance()
      Dim rhs As Integer = ParseMulDiv()
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

Public Function ParseExpr() As Integer
  Dim lhs As Integer = ParseAddSub()
  Dim k As Integer = PeekKind()
  If (k = TokenKind.TkEq) Or (k = TokenKind.TkNeq) Or (k = TokenKind.TkLt) Or (k = TokenKind.TkGt) Or (k = TokenKind.TkLe) Or (k = TokenKind.TkGe) Then
    Advance()
    Dim rhs As Integer = ParseAddSub()
    Dim bin As Integer = NewNode(NodeKind.NdBinOp)
    g_nodes(bin).op = k
    g_nodes(bin).left = lhs
    g_nodes(bin).right = rhs
    Return bin
  End If
  Return lhs
End Function

Private Sub ParseBlock(stopOnElse As Boolean, ByRef outStart As Integer, ByRef outCount As Integer)
  Dim buf(128) As Integer
  Dim cnt As Integer = 0
  Dim running As Boolean = True
  While running Do
    SkipNewlines()
    Dim k As Integer = PeekKind()
    If (k = TokenKind.TkEnd) Or (k = TokenKind.TkEof) Then
      running = False
    Else
      If stopOnElse And (k = TokenKind.TkElse) Then
        running = False
      Else
        buf(cnt) = ParseStmt()
        cnt = cnt + 1
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

Public Function ParseStmt() As Integer
  Dim k As Integer = PeekKind()

  ' Dim x As Integer [= expr]
  If k = TokenKind.TkDim Then
    Advance()
    Dim namePos As Integer = g_tokens(p_pos).pos
    Dim nameLen As Integer = g_tokens(p_pos).len
    Expect(TokenKind.TkIdent)
    Expect(TokenKind.TkAs)
    Expect(TokenKind.TkInteger)
    Dim initNode As Integer = -1
    If PeekKind() = TokenKind.TkEq Then
      Advance()
      initNode = ParseExpr()
    End If
    Dim id As Integer = NewNode(NodeKind.NdDim)
    g_nodes(id).intVal = namePos
    g_nodes(id).lenVal = nameLen
    g_nodes(id).left = initNode
    Return id
  End If

  ' Return [expr]
  If k = TokenKind.TkReturn Then
    Advance()
    Dim id As Integer = NewNode(NodeKind.NdReturn)
    Dim nk As Integer = PeekKind()
    If (nk <> TokenKind.TkNewline) And (nk <> TokenKind.TkEof) And (nk <> TokenKind.TkEnd) Then
      g_nodes(id).left = ParseExpr()
    End If
    Return id
  End If

  ' If cond Then ... [Else ...] End If
  If k = TokenKind.TkIf Then
    Advance()
    Dim condId As Integer = ParseExpr()
    Expect(TokenKind.TkThen)
    Dim thenStart As Integer = 0
    Dim thenCount As Integer = 0
    ParseBlock(True, ByRef thenStart, ByRef thenCount)
    Dim elseStart As Integer = 0
    Dim elseCount As Integer = 0
    If PeekKind() = TokenKind.TkElse Then
      Advance()
      ParseBlock(False, ByRef elseStart, ByRef elseCount)
    End If
    Expect(TokenKind.TkEnd)
    Expect(TokenKind.TkIf)
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
    Advance()
    Dim condId As Integer = ParseExpr()
    Expect(TokenKind.TkDo)
    Dim bodyStart As Integer = 0
    Dim bodyCount As Integer = 0
    ParseBlock(False, ByRef bodyStart, ByRef bodyCount)
    Expect(TokenKind.TkEnd)
    Expect(TokenKind.TkWhile)
    Dim id As Integer = NewNode(NodeKind.NdWhile)
    g_nodes(id).left = condId
    g_nodes(id).firstChild = bodyStart
    g_nodes(id).childCount = bodyCount
    Return id
  End If

  ' Assignment: ident = expr
  If (k = TokenKind.TkIdent) And (PeekNextKind() = TokenKind.TkEq) Then
    Dim namePos As Integer = g_tokens(p_pos).pos
    Dim nameLen As Integer = g_tokens(p_pos).len
    Advance()
    Advance()
    Dim rhs As Integer = ParseExpr()
    Dim id As Integer = NewNode(NodeKind.NdAssign)
    g_nodes(id).intVal = namePos
    g_nodes(id).lenVal = nameLen
    g_nodes(id).left = rhs
    Return id
  End If

  ' Expression statement (e.g. function call)
  Dim eId As Integer = ParseExpr()
  Dim stmtId As Integer = NewNode(NodeKind.NdExprStmt)
  g_nodes(stmtId).left = eId
  Return stmtId
End Function

Private Function ParseFunc() As Integer
  Dim isPublic As Integer = 0
  If PeekKind() = TokenKind.TkPublic Then
    isPublic = 1
    Advance()
  End If
  Dim isSub As Integer = 0
  If PeekKind() = TokenKind.TkSub Then
    isSub = 1
    Advance()
  Else
    Expect(TokenKind.TkFunction)
  End If

  Dim namePos As Integer = g_tokens(p_pos).pos
  Dim nameLen As Integer = g_tokens(p_pos).len
  Expect(TokenKind.TkIdent)
  Expect(TokenKind.TkLParen)

  Dim paramBuf(16) As Integer
  Dim paramCount As Integer = 0
  If PeekKind() <> TokenKind.TkRParen Then
    Dim reading As Boolean = True
    While reading Do
      Dim pPos As Integer = g_tokens(p_pos).pos
      Dim pLen As Integer = g_tokens(p_pos).len
      Expect(TokenKind.TkIdent)
      Expect(TokenKind.TkAs)
      Expect(TokenKind.TkInteger)
      Dim pNode As Integer = NewNode(NodeKind.NdParam)
      g_nodes(pNode).intVal = pPos
      g_nodes(pNode).lenVal = pLen
      paramBuf(paramCount) = pNode
      paramCount = paramCount + 1
      If PeekKind() = TokenKind.TkComma Then
        Advance()
      Else
        reading = False
      End If
    End While
  End If
  Expect(TokenKind.TkRParen)

  If isSub = 0 Then
    Expect(TokenKind.TkAs)
    Expect(TokenKind.TkInteger)
  End If

  Dim bodyStart As Integer = 0
  Dim bodyCount As Integer = 0
  ParseBlock(False, ByRef bodyStart, ByRef bodyCount)

  Expect(TokenKind.TkEnd)
  If isSub = 1 Then
    Expect(TokenKind.TkSub)
  Else
    Expect(TokenKind.TkFunction)
  End If

  Dim fnId As Integer = NewNode(NodeKind.NdFunc)
  g_nodes(fnId).op = isSub * 2 + isPublic
  g_nodes(fnId).intVal = namePos
  g_nodes(fnId).lenVal = nameLen
  g_nodes(fnId).firstChild = bodyStart
  g_nodes(fnId).childCount = bodyCount
  g_nodes(fnId).extraStart = g_kidCount
  g_nodes(fnId).extraCount = paramCount
  Dim i As Integer = 0
  While i < paramCount Do
    PushKid(paramBuf(i))
    i = i + 1
  End While
  Return fnId
End Function

Public Function ParseProgram() As Integer
  p_pos = 0
  Dim fnBuf(64) As Integer
  Dim fnCount As Integer = 0
  SkipNewlines()
  While PeekKind() <> TokenKind.TkEof Do
    fnBuf(fnCount) = ParseFunc()
    fnCount = fnCount + 1
    SkipNewlines()
  End While
  Dim progId As Integer = NewNode(NodeKind.NdProgram)
  g_nodes(progId).firstChild = g_kidCount
  g_nodes(progId).childCount = fnCount
  Dim i As Integer = 0
  While i < fnCount Do
    PushKid(fnBuf(i))
    i = i + 1
  End While
  Return progId
End Function
