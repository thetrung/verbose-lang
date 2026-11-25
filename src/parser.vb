' VerBose Self-Hosting Compiler (verbose) - Full Recursive-Descent Parser

Include "constants.vb"
Include "lexer.vb"
Include "ast.vb"

Private Static p_pos = 0, p_errors = 0, p_withDepth = 0 As Integer

Private Function PPeekKind() As Integer
  If p_pos >= g_tokCount Then
    Return TkEof
  End If
  Return g_tokens(p_pos).kind
End Function

Private Function PPeekNextKind() As Integer
  If (p_pos + 1) >= g_tokCount Then
    Return TkEof
  End If
  Return g_tokens(p_pos + 1).kind
End Function

Private Sub PAdvance()
  If p_pos < g_tokCount Then
    p_pos = p_pos + 1
  End If
End Sub

Private Sub PSkipNewlines()
  While PPeekKind() = TkNewline Do
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
  Match k With
    Case TkInteger Then outKind = TyInt
    Case TkByte Then outKind = TyByte
    Case TkShort Then outKind = TyShort
    Case TkLong Then outKind = TyLong
    Case TkBoolean Then outKind = TyBoolean
    Case TkPointer, TkString Then outKind = TyPointer
    Case TkArena Then outKind = TyArena
    Case TkNothing Then outKind = TyNothing
    Case TkSingle Then outKind = TySingle
    Case TkDouble Then outKind = TyDouble
    Case TkIdent Then
      outKind = TyStruct
      outNamePos = g_tokens(p_pos).pos
      outNameLen = g_tokens(p_pos).len
    Case _ Then
      Printf(MSG_PARSE_EXPECT_TYPE, k, g_tokens(p_pos).line)
      p_errors = p_errors + 1
      outKind = TyInt
  End Match
  PAdvance()
End Sub

Private Sub PParseTypeSpec(ByRef outKind, outInnerKind, outNamePos, outNameLen As Integer)
  outInnerKind = TyNothing
  outNamePos = 0
  outNameLen = 0
  If PPeekKind() = TkTask Then
    outKind = TyTask
    PAdvance()
    If PPeekKind() = TkLParen Then
      PAdvance()
      PParseBaseType(ByRef outInnerKind, ByRef outNamePos, ByRef outNameLen)
      PExpect(TkRParen)
    End If
    Return
  End If
  PParseBaseType(ByRef outKind, ByRef outNamePos, ByRef outNameLen)
End Sub

Private Function PParseCallArg() As Integer
  Dim isByRef As Integer = 0
  If PPeekKind() = TkByRef Then
    isByRef = 1
    PAdvance()
  End If
  Dim eId As Integer = ParseExpr()
  Dim argId As Integer = NewNode(NdArg)
  g_nodes(argId).op = isByRef
  g_nodes(argId).left = eId
  Return argId
End Function

Private Function PParseWithInit(structNamePos, structNameLen As Integer) As Integer
  PExpect(TkWith)
  PExpect(TkLParen)
  PSkipNewlines()
  Dim fiBuf(64) As Integer, fiCount = 0 As Integer
  While (PPeekKind() <> TkRParen) And (PPeekKind() <> TkEof) Do
    Dim fPos = g_tokens(p_pos).pos, fLen = g_tokens(p_pos).len As Integer
    PExpect(TkIdent)
    PExpect(TkEq)
    Dim valExpr As Integer = ParseExpr()
    Dim fiId As Integer = NewNode(NdFieldInit)
    g_nodes(fiId).intVal = fPos
    g_nodes(fiId).lenVal = fLen
    g_nodes(fiId).left = valExpr
    fiBuf(fiCount) = fiId
    fiCount = fiCount + 1
    PSkipNewlines()
    If PPeekKind() = TkComma Then
      PAdvance()
      PSkipNewlines()
    End If
  End While
  PExpect(TkRParen)

  Dim initId As Integer = NewNode(NdStructInit)
  With g_nodes(initId)
    .intVal = structNamePos
    .lenVal = structNameLen
    .firstChild = g_kidCount
    .childCount = fiCount
  End With
  For i = 0 To fiCount - 1 Do
    PushKid(fiBuf(i))
  End For
  Return initId
End Function

Private Function PParseArrayLit() As Integer
  PExpect(TkLParen)
  PSkipNewlines()
  Dim elBuf(64) As Integer
  Dim elCount As Integer = 0
  If PPeekKind() <> TkRParen Then
    Dim reading As Boolean = True
    While reading Do
      elBuf(elCount) = ParseExpr()
      elCount = elCount + 1
      PSkipNewlines()
      If PPeekKind() = TkComma Then
        PAdvance()
        PSkipNewlines()
      Else
        reading = False
      End If
    End While
  End If
  PExpect(TkRParen)
  Dim litId As Integer = NewNode(NdArrayLit)
  g_nodes(litId).firstChild = g_kidCount
  g_nodes(litId).childCount = elCount
  For i = 0 To elCount - 1 Do
    PushKid(elBuf(i))
  End For
  Return litId
End Function

Private Function PParsePrimary() As Integer
  Dim k As Integer = PPeekKind()

  If k = TkIntLit Then
    Dim id As Integer = NewNode(NdIntLit)
    g_nodes(id).intVal = g_tokens(p_pos).intVal
    PAdvance()
    Return id
  End If

  If k = TkFloatLit Then
    Dim id As Integer = NewNode(NdFloatLit)
    g_nodes(id).intVal = g_tokens(p_pos).pos
    g_nodes(id).lenVal = g_tokens(p_pos).len
    PAdvance()
    Return id
  End If

  If k = TkTrue Then
    Dim id As Integer = NewNode(NdBoolLit)
    g_nodes(id).intVal = 1
    PAdvance()
    Return id
  End If

  If k = TkFalse Then
    Dim id As Integer = NewNode(NdBoolLit)
    g_nodes(id).intVal = 0
    PAdvance()
    Return id
  End If

  If k = TkStrLit Then
    Dim id As Integer = NewNode(NdStrLit)
    g_nodes(id).intVal = g_tokens(p_pos).pos
    g_nodes(id).lenVal = g_tokens(p_pos).len
    PAdvance()
    Return id
  End If

  If k = TkInterpStart Then
    PAdvance()
    Dim partBuf(64) As Integer
    Dim partCount As Integer = 0
    While (PPeekKind() <> TkInterpEnd) And (PPeekKind() <> TkEof) Do
      If PPeekKind() = TkStrLit Then
        Dim sId As Integer = NewNode(NdStrLit)
        g_nodes(sId).intVal = g_tokens(p_pos).pos
        g_nodes(sId).lenVal = g_tokens(p_pos).len
        PAdvance()
        If partCount < 64 Then
          partBuf(partCount) = sId
          partCount = partCount + 1
        End If
      Else
        If PPeekKind() = TkInterpExprStart Then
          PAdvance()
          PSkipNewlines()
          Dim eId As Integer = ParseExpr()
          PSkipNewlines()
          PExpect(TkInterpExprEnd)
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
    PExpect(TkInterpEnd)
    If partCount = 0 Then
      Dim emptyId As Integer = NewNode(NdStrLit)
      g_nodes(emptyId).intVal = 0
      g_nodes(emptyId).lenVal = 0
      Return emptyId
    End If
    If (partCount = 1) And (g_nodes(partBuf(0)).kind = NdStrLit) Then
      Return partBuf(0)
    End If
    Dim id As Integer = NewNode(NdStrInterp)
    g_nodes(id).firstChild = g_kidCount
    g_nodes(id).childCount = partCount
    For pi = 0 To partCount - 1 Do
      PushKid(partBuf(pi))
    End For
    Return id
  End If

  If k = TkLParen Then
    PAdvance()
    PSkipNewlines()
    If PPeekKind() = TkRParen Then
      PAdvance()
      Dim emptyLit As Integer = NewNode(NdArrayLit)
      Return emptyLit
    End If
    Dim first As Integer = ParseExpr()
    PSkipNewlines()
    If PPeekKind() = TkComma Then
      Dim elBuf(64) As Integer
      elBuf(0) = first
      Dim elCount As Integer = 1
      While PPeekKind() = TkComma Do
        PAdvance()
        PSkipNewlines()
        elBuf(elCount) = ParseExpr()
        elCount = elCount + 1
        PSkipNewlines()
      End While
      PExpect(TkRParen)
      Dim litId As Integer = NewNode(NdArrayLit)
      g_nodes(litId).firstChild = g_kidCount
      g_nodes(litId).childCount = elCount
      Dim i As Integer = 0
      While i < elCount Do
        PushKid(elBuf(i))
        i = i + 1
      End While
      Return litId
    End If
    PExpect(TkRParen)
    Return first
  End If

  If k = TkSizeOf Then
    PAdvance()
    PExpect(TkLParen)
    Dim tk, tik, tnPos, tnLen As Integer = 0
    PParseTypeSpec(ByRef tk, ByRef tik, ByRef tnPos, ByRef tnLen)
    PExpect(TkRParen)
    Dim id As Integer = NewNode(NdSizeOf)
    With g_nodes(id)
      .tyKind = tk
      .tyInnerKind = tik
      .tyNamePos = tnPos
      .tyNameLen = tnLen
    End With
    Return id
  End If

  If k = TkArenaAlloc Then
    PAdvance()
    PExpect(TkLParen)
    Dim arenaArg As Integer = PParseCallArg()
    PExpect(TkComma)
    Dim tk, tik, tnPos, tnLen As Integer = 0
    PParseTypeSpec(ByRef tk, ByRef tik, ByRef tnPos, ByRef tnLen)
    Dim cntExpr As Integer = -1
    If PPeekKind() = TkComma Then
      PAdvance()
      cntExpr = ParseExpr()
    Else
      cntExpr = NewNode(NdIntLit)
      g_nodes(cntExpr).intVal = 1
    End If
    PExpect(TkRParen)
    Dim id As Integer = NewNode(NdArenaAlloc)
    With g_nodes(id)
      .op = g_nodes(arenaArg).op
      .left = g_nodes(arenaArg).left
      .right = cntExpr
      .tyKind = tk
      .tyInnerKind = tik
      .tyNamePos = tnPos
      .tyNameLen = tnLen
    End With
    Return id
  End If

  If k = TkHeapAlloc Then
    PAdvance()
    PExpect(TkLParen)
    Dim tk, tik, tnPos, tnLen As Integer = 0
    PParseTypeSpec(ByRef tk, ByRef tik, ByRef tnPos, ByRef tnLen)
    Dim cntExpr As Integer = -1
    If PPeekKind() = TkComma Then
      PAdvance()
      cntExpr = ParseExpr()
    Else
      cntExpr = NewNode(NdIntLit)
      g_nodes(cntExpr).intVal = 1
    End If
    PExpect(TkRParen)
    Dim id As Integer = NewNode(NdHeapAlloc)
    With g_nodes(id)
      .left = cntExpr
      .tyKind = tk
      .tyInnerKind = tik
      .tyNamePos = tnPos
      .tyNameLen = tnLen
    End With
    Return id
  End If

  If k = TkHeapRealloc Then
    PAdvance()
    PExpect(TkLParen)
    Dim ptrExpr As Integer = ParseExpr()
    PExpect(TkComma)
    Dim tk, tik, tnPos, tnLen As Integer = 0
    PParseTypeSpec(ByRef tk, ByRef tik, ByRef tnPos, ByRef tnLen)
    PExpect(TkComma)
    Dim cntExpr As Integer = ParseExpr()
    PExpect(TkRParen)
    Dim id As Integer = NewNode(NdHeapRealloc)
    With g_nodes(id)
      .left = ptrExpr
      .right = cntExpr
      .tyKind = tk
      .tyInnerKind = tik
      .tyNamePos = tnPos
      .tyNameLen = tnLen
    End With
    Return id
  End If

  If k = TkVolatileLoad Then
    PAdvance()
    PExpect(TkLParen)
    Dim addrExpr As Integer = ParseExpr()
    PExpect(TkComma)
    Dim tk, tik, tnPos, tnLen As Integer = 0
    PParseTypeSpec(ByRef tk, ByRef tik, ByRef tnPos, ByRef tnLen)
    PExpect(TkRParen)
    Dim id As Integer = NewNode(NdVolatileLoad)
    With g_nodes(id)
      .left = addrExpr
      .tyKind = tk
      .tyInnerKind = tik
      .tyNamePos = tnPos
      .tyNameLen = tnLen
    End With
    Return id
  End If

  If k = TkVolatileStore Then
    PAdvance()
    PExpect(TkLParen)
    Dim addrExpr As Integer = ParseExpr()
    PExpect(TkComma)
    Dim tk, tik, tnPos, tnLen As Integer = 0
    PParseTypeSpec(ByRef tk, ByRef tik, ByRef tnPos, ByRef tnLen)
    PExpect(TkComma)
    Dim valExpr As Integer = ParseExpr()
    PExpect(TkRParen)
    Dim id As Integer = NewNode(NdVolatileStore)
    With g_nodes(id)
      .left = addrExpr
      .right = valExpr
      .tyKind = tk
      .tyInnerKind = tik
      .tyNamePos = tnPos
      .tyNameLen = tnLen
    End With
    Return id
  End If

  If k = TkAddressOf Then
    PAdvance()
    PExpect(TkLParen)
    Dim targetExpr As Integer = ParseExpr()
    PExpect(TkRParen)
    Dim id As Integer = NewNode(NdAddressOf)
    g_nodes(id).left = targetExpr
    Return id
  End If

  If k = TkPtrRead Then
    PAdvance()
    PExpect(TkLParen)
    Dim addrExpr As Integer = ParseExpr()
    PExpect(TkComma)
    Dim tk, tik, tnPos, tnLen As Integer = 0
    PParseTypeSpec(ByRef tk, ByRef tik, ByRef tnPos, ByRef tnLen)
    PExpect(TkRParen)
    Dim id As Integer = NewNode(NdPtrRead)
    With g_nodes(id)
      .left = addrExpr
      .tyKind = tk
      .tyInnerKind = tik
      .tyNamePos = tnPos
      .tyNameLen = tnLen
    End With
    Return id
  End If

  If k = TkPtrWrite Then
    PAdvance()
    PExpect(TkLParen)
    Dim addrExpr As Integer = ParseExpr()
    PExpect(TkComma)
    Dim tk, tik, tnPos, tnLen As Integer = 0
    PParseTypeSpec(ByRef tk, ByRef tik, ByRef tnPos, ByRef tnLen)
    PExpect(TkComma)
    Dim valExpr As Integer = ParseExpr()
    PExpect(TkRParen)
    Dim id As Integer = NewNode(NdPtrWrite)
    With g_nodes(id)
      .left = addrExpr
      .right = valExpr
      .tyKind = tk
      .tyInnerKind = tik
      .tyNamePos = tnPos
      .tyNameLen = tnLen
    End With
    Return id
  End If

  If k = TkPtrAdd Then
    PAdvance()
    PExpect(TkLParen)
    Dim ptrExpr As Integer = ParseExpr()
    PExpect(TkComma)
    Dim offExpr As Integer = ParseExpr()
    PExpect(TkRParen)
    Dim id As Integer = NewNode(NdPtrAdd)
    g_nodes(id).left = ptrExpr
    g_nodes(id).right = offExpr
    Return id
  End If

  If (k = TkDot) And (p_withDepth > 0) Then
    Return NewNode(NdWithTarget)
  End If

  If k = TkIdent Then
    Dim namePos As Integer = g_tokens(p_pos).pos
    Dim nameLen As Integer = g_tokens(p_pos).len
    PAdvance()
    If (PPeekKind() = TkWith) And (PPeekNextKind() = TkLParen) Then
      Return PParseWithInit(namePos, nameLen)
    End If
    If PPeekKind() = TkLParen Then
      PAdvance()
      PSkipNewlines()
      Dim argBuf(32) As Integer
      Dim argCount As Integer = 0
      If PPeekKind() <> TkRParen Then
        Dim reading As Boolean = True
        While reading Do
          argBuf(argCount) = PParseCallArg()
          argCount = argCount + 1
          PSkipNewlines()
          If PPeekKind() = TkComma Then
            PAdvance()
            PSkipNewlines()
          Else
            reading = False
          End If
        End While
      End If
      PExpect(TkRParen)
      Dim callId As Integer = NewNode(NdCall)
      With g_nodes(callId)
        .intVal = namePos
        .lenVal = nameLen
        .firstChild = g_kidCount
        .childCount = argCount
      End With
      For i = 0 To argCount - 1 Do
        PushKid(argBuf(i))
      End For
      Return callId
    Else
      Dim varId As Integer = NewNode(NdVar)
      g_nodes(varId).intVal = namePos
      g_nodes(varId).lenVal = nameLen
      Return varId
    End If
  End If

  Printf(MSG_PARSE_UNEXPECTED_PRIMARY, k, g_tokens(p_pos).line)
  p_errors = p_errors + 1
  PAdvance()
  Return NewNode(NdIntLit)
End Function

Private Function PParsePostfix() As Integer
  Dim cur As Integer = PParsePrimary()
  While (PPeekKind() = TkDot) Or (PPeekKind() = TkLParen) Do
    If PPeekKind() = TkDot Then
      PAdvance()
      Dim fPos As Integer = g_tokens(p_pos).pos
      Dim fLen As Integer = g_tokens(p_pos).len
      PExpect(TkIdent)
      Dim fa As Integer = NewNode(NdFieldAccess)
      g_nodes(fa).left = cur
      g_nodes(fa).intVal = fPos
      g_nodes(fa).lenVal = fLen
      cur = fa
    Else
      PAdvance()
      PSkipNewlines()
      Dim idxE As Integer = ParseExpr()
      PSkipNewlines()
      PExpect(TkRParen)
      Dim ia As Integer = NewNode(NdIndexAccess)
      g_nodes(ia).left = cur
      g_nodes(ia).right = idxE
      cur = ia
    End If
  End While
  Return cur
End Function

Private Function PParseUnary() As Integer
  If PPeekKind() = TkMinus Then
    PAdvance()
    Dim inner As Integer = PParseUnary()
    Dim id As Integer = NewNode(NdNeg)
    g_nodes(id).left = inner
    Return id
  End If
  If PPeekKind() = TkAwait Then
    PAdvance()
    Dim inner As Integer = PParseUnary()
    Dim id As Integer = NewNode(NdAwait)
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
    If (k = TkStar) Or (k = TkSlash) Or (k = TkMod) Then
      PAdvance()
      Dim rhs As Integer = PParseUnary()
      Dim bin As Integer = NewNode(NdBinOp)
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
    Dim bin As Integer = NewNode(NdBinOp)
    g_nodes(bin).op = TkAmp
    g_nodes(bin).left = lhs
    g_nodes(bin).right = rhs
    Return bin
  End If
  Dim startPos As Integer = g_srcPoolLen
  For i = 0 To l1 - 1 Do
    g_srcPool(g_srcPoolLen) = g_srcPool(p1 + i)
    g_srcPoolLen = g_srcPoolLen + 1
  End For
  For j = 0 To l2 - 1 Do
    g_srcPool(g_srcPoolLen) = g_srcPool(p2 + j)
    g_srcPoolLen = g_srcPoolLen + 1
  End For
  Dim id As Integer = NewNode(NdStrLit)
  g_nodes(id).intVal = startPos
  g_nodes(id).lenVal = l1 + l2
  Return id
End Function

Private Function PParseAddSub() As Integer
  Dim lhs As Integer = PParseMulDiv()
  Dim going As Boolean = True
  While going Do
    Dim k As Integer = PPeekKind()
    If (k = TkPlus) Or (k = TkMinus) Then
      PAdvance()
      Dim rhs As Integer = PParseMulDiv()
      If (k = TkPlus) And (g_nodes(lhs).kind = NdStrLit) And (g_nodes(rhs).kind = NdStrLit) Then
        lhs = PFoldStrLit(lhs, rhs)
      Else
        Dim bin As Integer = NewNode(NdBinOp)
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
  While PPeekKind() = TkAmp Do
    PAdvance()
    PSkipNewlines()
    Dim rhs As Integer = PParseAddSub()
    If (g_nodes(lhs).kind = NdStrLit) And (g_nodes(rhs).kind = NdStrLit) Then
      lhs = PFoldStrLit(lhs, rhs)
    Else
      Dim bin As Integer = NewNode(NdBinOp)
      g_nodes(bin).op = TkAmp
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
    If (k = TkShl) Or (k = TkShr) Then
      PAdvance()
      Dim rhs As Integer = PParseConcat()
      Dim bin As Integer = NewNode(NdBinOp)
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
  If (k = TkEq) Or (k = TkNeq) Or (k = TkLt) Or (k = TkGt) Or (k = TkLe) Or (k = TkGe) Then
    PAdvance()
    Dim rhs As Integer = PParseShift()
    Dim bin As Integer = NewNode(NdBinOp)
    g_nodes(bin).op = k
    g_nodes(bin).left = lhs
    g_nodes(bin).right = rhs
    Return bin
  End If
  Return lhs
End Function

Private Function PParseNot() As Integer
  If PPeekKind() = TkNot Then
    PAdvance()
    Dim inner As Integer = PParseNot()
    Dim id As Integer = NewNode(NdNot)
    g_nodes(id).left = inner
    Return id
  End If
  Return PParseCmp()
End Function

Private Function PParseAnd() As Integer
  Dim lhs As Integer = PParseNot()
  While PPeekKind() = TkAnd Do
    PAdvance()
    Dim rhs As Integer = PParseNot()
    Dim bin As Integer = NewNode(NdBinOp)
    g_nodes(bin).op = TkAnd
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
    If (k = TkOr) Or (k = TkXor) Then
      PAdvance()
      Dim rhs As Integer = PParseAnd()
      Dim bin As Integer = NewNode(NdBinOp)
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
    Dim nPos(64), nLen(64), aLen(64), iInit(64) As Integer
    Dim itemCnt = 0 As Integer, moreItems = True As Boolean
    While moreItems Do
      nPos(itemCnt) = g_tokens(p_pos).pos
      nLen(itemCnt) = g_tokens(p_pos).len
      PExpect(TkIdent)
      Dim arrLen As Integer = 0
      If PPeekKind() = TkLParen Then
        PAdvance()
        If PPeekKind() = TkRParen Then
          arrLen = -1
          PAdvance()
        Else
          arrLen = g_tokens(p_pos).intVal
          PExpect(TkIntLit)
          PExpect(TkRParen)
          If arrLen <= 0 Then
            arrLen = -2
          End If
        End If
      End If
      Dim itemInit As Integer = -1
      If PPeekKind() = TkEq Then
        PAdvance()
        If (arrLen <> 0) And (PPeekKind() = TkLParen) Then
          itemInit = PParseArrayLit()
        Else
          itemInit = ParseExpr()
        End If
        If (arrLen = -1) And (g_nodes(itemInit).kind = NdArrayLit) Then
          arrLen = g_nodes(itemInit).childCount
        End If
      End If
      aLen(itemCnt) = arrLen
      iInit(itemCnt) = itemInit
      itemCnt = itemCnt + 1
      If PPeekKind() = TkComma Then
        PAdvance()
        PSkipNewlines()
      Else
        moreItems = False
      End If
    End While

    PExpect(TkAs)
    Dim tk As Integer = 0
    Dim tik As Integer = 0
    Dim tnPos As Integer = 0
    Dim tnLen As Integer = 0
    PParseTypeSpec(ByRef tk, ByRef tik, ByRef tnPos, ByRef tnLen)

    Dim atExpr As Integer = -1
    If PPeekKind() = TkIdent Then
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
    If PPeekKind() = TkWith Then
      sharedInit = PParseWithInit(tnPos, tnLen)
    Else
      If (firstArrLen = 0) And (tk = TyStruct) And (PPeekKind() = TkLParen) Then
        PAdvance()
        PSkipNewlines()
        Dim argBuf(32) As Integer
        Dim argCount As Integer = 0
        If PPeekKind() <> TkRParen Then
          Dim reading As Boolean = True
          While reading Do
            argBuf(argCount) = PParseCallArg()
            argCount = argCount + 1
            PSkipNewlines()
            If PPeekKind() = TkComma Then
              PAdvance()
              PSkipNewlines()
            Else
              reading = False
            End If
          End While
        End If
        PExpect(TkRParen)
        Dim callId As Integer = NewNode(NdCall)
        With g_nodes(callId)
          .intVal = tnPos
          .lenVal = tnLen
          .firstChild = g_kidCount
          .childCount = argCount
        End With
        For ci = 0 To argCount - 1 Do
          PushKid(argBuf(ci))
        End For
        sharedInit = callId
      Else
        If PPeekKind() = TkEq Then
          PAdvance()
          If (firstArrLen <> 0) And (PPeekKind() = TkLParen) Then
            sharedInit = PParseArrayLit()
          Else
            sharedInit = ParseExpr()
          End If
        End If
      End If
    End If

    For vi = 0 To itemCnt - 1 Do
      Dim curArrLen As Integer = aLen(vi)
      Dim curInit As Integer = iInit(vi)
      If curInit < 0 Then
        curInit = sharedInit
      End If
      If (curArrLen = -1) And (curInit >= 0) And (g_nodes(curInit).kind = NdArrayLit) Then
        curArrLen = g_nodes(curInit).childCount
      End If
      Dim id As Integer = NewNode(ndKind)
      If ndKind = NdGlobalVar Then
        g_nodes(id).op = vis
        g_nodes(id).modId = defModId
      End If
      With g_nodes(id)
        .intVal = nPos(vi)
        .lenVal = nLen(vi)
        .left = curInit
        .right = atExpr
        .flags = isVolatile
        .tyKind = tk
        .tyInnerKind = tik
        .tyArrLen = curArrLen
        .tyNamePos = tnPos
        .tyNameLen = tnLen
      End With
      outBuf(outCnt) = id
      outCnt = outCnt + 1
    End For

    If PPeekKind() = TkComma Then
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
    Match With
      Case (k = TkEnd) Or (k = TkEof) Then running = False
      Case (stopMode = 1) And (k = TkElse) Then running = False
      Case (stopMode = 2) And (k = TkCase) Then running = False
      Case _ Then PParseStmtInto(buf, ByRef cnt)
    End Match
  End While
  outStart = g_kidCount
  outCount = cnt
  For i = 0 To cnt - 1 Do
    PushKid(buf(i))
  End For
End Sub

Private Sub PParseStmtInto(outBuf() As Integer, ByRef outCnt As Integer)
  Dim k As Integer = PPeekKind()

  ' [Volatile] Dim / Static name[(N | )] [= init], ... As Type [At addr] [= expr | With ( ... ) | (args)], ...
  Dim isVol As Integer = 0
  If k = TkVolatile Then
    isVol = 1
    PAdvance()
    k = PPeekKind()
  End If
  If (k = TkDim) Or (k = TkStatic) Then
    Dim ndKind As Integer = NdDim
    If k = TkStatic Then
      ndKind = NdStaticDim
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
  If k = TkReturn Then
    PAdvance()
    Dim id As Integer = NewNode(NdReturn)
    Dim nk As Integer = PPeekKind()
    If (nk <> TkNewline) And (nk <> TkEof) And (nk <> TkEnd) Then
      g_nodes(id).left = ParseExpr()
    End If
    Return id
  End If

  ' Label name
  If k = TkLabel Then
    PAdvance()
    Dim lPos As Integer = g_tokens(p_pos).pos
    Dim lLen As Integer = g_tokens(p_pos).len
    PExpect(TkIdent)
    Dim id As Integer = NewNode(NdLabel)
    g_nodes(id).intVal = lPos
    g_nodes(id).lenVal = lLen
    Return id
  End If

  ' GoTo name
  If k = TkGoTo Then
    PAdvance()
    Dim lPos As Integer = g_tokens(p_pos).pos
    Dim lLen As Integer = g_tokens(p_pos).len
    PExpect(TkIdent)
    Dim id As Integer = NewNode(NdGoTo)
    g_nodes(id).intVal = lPos
    g_nodes(id).lenVal = lLen
    Return id
  End If

  ' Break
  If k = TkBreak Then
    PAdvance()
    Return NewNode(NdBreak)
  End If

  ' Continue
  If k = TkContinue Then
    PAdvance()
    Return NewNode(NdContinue)
  End If

  ' Await expr (statement form for Async Sub / Task)
  If k = TkAwait Then
    Dim awExpr As Integer = PParseUnary()
    Dim stmtId As Integer = NewNode(NdExprStmt)
    g_nodes(stmtId).left = awExpr
    Return stmtId
  End If

  ' Assert cond [, msg]
  If k = TkAssert Then
    Dim lineNum As Integer = g_tokens(p_pos).line
    PAdvance()
    Dim condId As Integer = ParseExpr()
    Dim msgId As Integer = -1
    If PPeekKind() = TkComma Then
      PAdvance()
      msgId = ParseExpr()
    End If
    Dim id As Integer = NewNode(NdAssert)
    g_nodes(id).intVal = lineNum
    g_nodes(id).left = condId
    g_nodes(id).right = msgId
    Return id
  End If

  ' If cond Then ... [Else ...] End If
  If k = TkIf Then
    PAdvance()
    Dim condId As Integer = ParseExpr()
    PExpect(TkThen)
    Dim thenStart As Integer = 0
    Dim thenCount As Integer = 0
    PParseBlock(1, ByRef thenStart, ByRef thenCount)
    Dim elseStart As Integer = 0
    Dim elseCount As Integer = 0
    If PPeekKind() = TkElse Then
      PAdvance()
      PParseBlock(0, ByRef elseStart, ByRef elseCount)
    End If
    PExpect(TkEnd)
    PExpect(TkIf)
    Dim id As Integer = NewNode(NdIf)
    With g_nodes(id)
      .left = condId
      .firstChild = thenStart
      .childCount = thenCount
      .extraStart = elseStart
      .extraCount = elseCount
    End With
    Return id
  End If

  ' While cond Do ... End While
  If k = TkWhile Then
    PAdvance()
    Dim condId As Integer = ParseExpr()
    PExpect(TkDo)
    Dim bodyStart As Integer = 0
    Dim bodyCount As Integer = 0
    PParseBlock(0, ByRef bodyStart, ByRef bodyCount)
    PExpect(TkEnd)
    PExpect(TkWhile)
    Dim id As Integer = NewNode(NdWhile)
    g_nodes(id).left = condId
    g_nodes(id).firstChild = bodyStart
    g_nodes(id).childCount = bodyCount
    Return id
  End If

  ' With target ... End With
  If k = TkWith Then
    PAdvance()
    Dim tgtExpr As Integer = ParseExpr()
    p_withDepth = p_withDepth + 1
    Dim bodyStart As Integer = 0
    Dim bodyCount As Integer = 0
    PParseBlock(0, ByRef bodyStart, ByRef bodyCount)
    p_withDepth = p_withDepth - 1
    PExpect(TkEnd)
    PExpect(TkWith)
    Dim id As Integer = NewNode(NdWith)
    g_nodes(id).left = tgtExpr
    g_nodes(id).firstChild = bodyStart
    g_nodes(id).childCount = bodyCount
    Return id
  End If

  ' For var = start To finish Do ... End For
  If k = TkFor Then
    PAdvance()
    Dim varPos As Integer = g_tokens(p_pos).pos
    Dim varLen As Integer = g_tokens(p_pos).len
    PExpect(TkIdent)
    PExpect(TkEq)
    Dim startE As Integer = ParseExpr()
    PExpect(TkTo)
    Dim finishE As Integer = ParseExpr()
    PExpect(TkDo)
    Dim bodyStart As Integer = 0
    Dim bodyCount As Integer = 0
    PParseBlock(0, ByRef bodyStart, ByRef bodyCount)
    PExpect(TkEnd)
    PExpect(TkFor)
    Dim id As Integer = NewNode(NdFor)
    With g_nodes(id)
      .intVal = varPos
      .lenVal = varLen
      .left = startE
      .right = finishE
      .firstChild = bodyStart
      .childCount = bodyCount
    End With
    Return id
  End If

  ' Select Case target ... Case e1, e2 ... [Case Else ...] End Select (desugared to NdMatch)
  If k = TkSelect Then
    PAdvance()
    PExpect(TkCase)
    Dim targetE As Integer = ParseExpr()
    PSkipNewlines()
    Dim armBuf(64) As Integer
    Dim armCount As Integer = 0
    While PPeekKind() = TkCase Do
      PAdvance()
      Dim isWild As Integer = 0
      Dim valBuf(32) As Integer
      Dim valCount As Integer = 0
      Dim bStart As Integer = 0
      Dim bCount As Integer = 0
      If PPeekKind() = TkElse Then
        PAdvance()
        isWild = 1
        PParseBlock(0, ByRef bStart, ByRef bCount)
      Else
        Dim reading As Boolean = True
        While reading Do
          valBuf(valCount) = ParseExpr()
          valCount = valCount + 1
          If PPeekKind() = TkComma Then
            PAdvance()
          Else
            reading = False
          End If
        End While
        PParseBlock(2, ByRef bStart, ByRef bCount)
      End If
      Dim armId As Integer = NewNode(NdMatchArm)
      With g_nodes(armId)
        .op = isWild
        .flags = 0
        .left = -1
        .firstChild = bStart
        .childCount = bCount
        .extraStart = g_kidCount
        .extraCount = valCount
      End With
      Dim vi As Integer = 0
      While vi < valCount Do
        PushKid(valBuf(vi))
        vi = vi + 1
      End While
      armBuf(armCount) = armId
      armCount = armCount + 1
      PSkipNewlines()
    End While
    PExpect(TkEnd)
    PExpect(TkSelect)
    Dim matchId As Integer = NewNode(NdMatch)
    With g_nodes(matchId)
      .op = 0
      .left = targetE
      .firstChild = g_kidCount
      .childCount = armCount
    End With
    For bi = 0 To armCount - 1 Do
      PushKid(armBuf(bi))
    End For
    Return matchId
  End If

  ' Match [expr] With
  '   Case pat1 [, pat2 ...] [When guard] Then <stmt> | <block>
  '   Case _ [When guard] Then <stmt> | <block>
  '   Case Else [When guard] [Then] <stmt> | <block>
  ' End Match
  If k = TkMatch Then
    PAdvance()
    Dim isCondMatch As Integer = 0
    Dim scrutinee As Integer = -1
    If PPeekKind() = TkWith Then
      isCondMatch = 1
      scrutinee = NewNode(NdBoolLit)
      g_nodes(scrutinee).intVal = 1
    Else
      scrutinee = ParseExpr()
    End If
    PExpect(TkWith)
    PSkipNewlines()
    Dim armBuf(128) As Integer
    Dim armCount As Integer = 0
    While PPeekKind() = TkCase Do
      PAdvance()
      Dim isWild As Integer = 0
      Dim valBuf(32) As Integer
      Dim valCount As Integer = 0
      If PPeekKind() = TkElse Then
        isWild = 1
        PAdvance()
      Else
        If (PPeekKind() = TkIdent) And (g_tokens(p_pos).len = 1) And (g_srcPool(g_tokens(p_pos).pos) = 95) Then
          isWild = 1
          PAdvance()
        Else
          Dim reading As Boolean = True
          While reading Do
            valBuf(valCount) = ParseExpr()
            valCount = valCount + 1
            If PPeekKind() = TkComma Then
              PAdvance()
              PSkipNewlines()
            Else
              reading = False
            End If
          End While
        End If
      End If

      Dim guardExpr As Integer = -1
      If PPeekKind() = TkWhen Then
        PAdvance()
        guardExpr = ParseExpr()
      End If

      If PPeekKind() = TkThen Then
        PAdvance()
      Else
        If isWild = 0 Then
          PExpect(TkThen)
        End If
      End If

      Dim isInline As Integer = 0
      Dim bStart = 0, bCount = 0 As Integer
      If (PPeekKind() <> TkNewline) And (PPeekKind() <> TkEof) Then
        isInline = 1
        Dim sBuf(32) As Integer
        Dim sCnt As Integer = 0
        PParseStmtInto(sBuf, ByRef sCnt)
        bStart = g_kidCount
        bCount = sCnt
        For si = 0 To sCnt - 1 Do
          PushKid(sBuf(si))
        End For
      Else
        PParseBlock(2, ByRef bStart, ByRef bCount)
      End If

      Dim armId As Integer = NewNode(NdMatchArm)
      With g_nodes(armId)
        .op = isWild
        .flags = isInline
        .left = guardExpr
        .firstChild = bStart
        .childCount = bCount
        .extraStart = g_kidCount
        .extraCount = valCount
      End With
      For vi = 0 To valCount - 1 Do
        PushKid(valBuf(vi))
      End For
      If armCount < 128 Then
        armBuf(armCount) = armId
        armCount = armCount + 1
      End If
      PSkipNewlines()
    End While
    PExpect(TkEnd)
    PExpect(TkMatch)
    Dim matchId As Integer = NewNode(NdMatch)
    With g_nodes(matchId)
      .op = isCondMatch
      .left = scrutinee
      .firstChild = g_kidCount
      .childCount = armCount
    End With
    For ai = 0 To armCount - 1 Do
      PushKid(armBuf(ai))
    End For
    Return matchId
  End If

  ' Postfix expression or Assignment (id = e, a.b = e, arr(i) = e, r.data(i) = e)
  Dim lhs As Integer = PParsePostfix()
  If PPeekKind() = TkEq Then
    PAdvance()
    Dim rhs As Integer = ParseExpr()
    Dim lk As Integer = g_nodes(lhs).kind
    If lk = NdVar Then
      Dim id As Integer = NewNode(NdAssign)
      g_nodes(id).intVal = g_nodes(lhs).intVal
      g_nodes(id).lenVal = g_nodes(lhs).lenVal
      g_nodes(id).left = rhs
      Return id
    End If
    If lk = NdFieldAccess Then
      Dim id As Integer = NewNode(NdFieldAssign)
      With g_nodes(id)
        .left = g_nodes(lhs).left
        .intVal = g_nodes(lhs).intVal
        .lenVal = g_nodes(lhs).lenVal
        .right = rhs
      End With
      Return id
    End If
    If lk = NdIndexAccess Then
      Dim id As Integer = NewNode(NdIndexAssign)
      g_nodes(id).left = g_nodes(lhs).left
      g_nodes(id).right = g_nodes(lhs).right
      g_nodes(id).third = rhs
      Return id
    End If
    If (lk = NdCall) And (g_nodes(lhs).childCount = 1) Then
      Dim baseVar As Integer = NewNode(NdVar)
      g_nodes(baseVar).intVal = g_nodes(lhs).intVal
      g_nodes(baseVar).lenVal = g_nodes(lhs).lenVal
      Dim argNode As Integer = g_kids(g_nodes(lhs).firstChild)
      Dim id As Integer = NewNode(NdIndexAssign)
      g_nodes(id).left = baseVar
      g_nodes(id).right = g_nodes(argNode).left
      g_nodes(id).third = rhs
      Return id
    End If
    Printf(MSG_PARSE_INVALID_ASSIGN, g_tokens(p_pos).line)
    p_errors = p_errors + 1
  End If

  Dim stmtId As Integer = NewNode(NdExprStmt)
  g_nodes(stmtId).left = lhs
  Return stmtId
End Function

' Parses parameter list with optional same-type grouping:
'   (a, b As Integer, ByRef x, y As Single, ...)
Private Sub PParseParamList(allowEllipsis As Integer, paramBuf() As Integer, ByRef paramCount, outIsVar As Integer)
  paramCount = 0
  outIsVar = 0
  PSkipNewlines()
  If PPeekKind() = TkRParen Then
    Return
  End If
  Dim grpStart As Integer = 0
  Dim grpByRef As Integer = 0
  Dim reading As Boolean = True
  While reading Do
    PSkipNewlines()
    If (allowEllipsis = 1) And (PPeekKind() = TkEllipsis) Then
      If paramCount > grpStart Then
        PExpect(TkAs)
      End If
      outIsVar = 1
      PAdvance()
      reading = False
    Else
      Dim isByRef As Integer = grpByRef
      If PPeekKind() = TkByRef Then
        isByRef = 1
        grpByRef = 1
        PAdvance()
      End If
      Dim pPos As Integer = g_tokens(p_pos).pos
      Dim pLen As Integer = g_tokens(p_pos).len
      PExpect(TkIdent)
      Dim arrLen As Integer = 0
      If PPeekKind() = TkLParen Then
        PAdvance()
        If PPeekKind() = TkRParen Then
          arrLen = -1
          PAdvance()
        Else
          arrLen = g_tokens(p_pos).intVal
          PExpect(TkIntLit)
          PExpect(TkRParen)
        End If
      End If
      Dim pNode As Integer = NewNode(NdParam)
      With g_nodes(pNode)
        .op = isByRef
        .intVal = pPos
        .lenVal = pLen
        .tyArrLen = arrLen
      End With
      paramBuf(paramCount) = pNode
      paramCount = paramCount + 1

      If PPeekKind() = TkAs Then
        PAdvance()
        Dim tk As Integer = 0
        Dim tik As Integer = 0
        Dim tnPos As Integer = 0
        Dim tnLen As Integer = 0
        PParseTypeSpec(ByRef tk, ByRef tik, ByRef tnPos, ByRef tnLen)
        For gi = grpStart To paramCount - 1 Do
          Dim pn As Integer = paramBuf(gi)
          With g_nodes(pn)
            .tyKind = tk
            .tyInnerKind = tik
            .tyNamePos = tnPos
            .tyNameLen = tnLen
          End With
        End For
        grpStart = paramCount
        grpByRef = 0
      End If

      PSkipNewlines()
      If PPeekKind() = TkComma Then
        PAdvance()
        PSkipNewlines()
      Else
        reading = False
      End If
    End If
  End While
  If paramCount > grpStart Then
    PExpect(TkAs)
  End If
End Sub

Private Function PIsOverloadableOp(k As Integer) As Boolean
  Match k With
    Case TkPlus, TkMinus, TkStar, TkSlash, TkMod, TkAmp, TkEq, TkNeq, TkLt, TkGt, TkLe, TkGe, TkAnd, TkOr, TkXor, TkShl, TkShr, TkNot Then Return True
    Case _ Then Return False
  End Match
End Function

Private Sub PParseDefInto(defBuf() As Integer, ByRef defCount As Integer)
  Dim defModId As Integer = g_tokens(p_pos).modId
  Dim vis As Integer = 0
  If PPeekKind() = TkPublic Then
    vis = 1
    PAdvance()
  Else
    If PPeekKind() = TkPrivate Then
      vis = 2
      PAdvance()
    End If
  End If

  Dim k As Integer = PPeekKind()

  ' Structure Name ... End Structure (supports grouped Dim a, b As T, c As U)
  If k = TkStructure Then
    PAdvance()
    Dim sPos As Integer = g_tokens(p_pos).pos
    Dim sLen As Integer = g_tokens(p_pos).len
    PExpect(TkIdent)
    PSkipNewlines()
    Dim fBuf(128) As Integer
    Dim fCount As Integer = 0
    While (PPeekKind() <> TkEnd) And (PPeekKind() <> TkEof) Do
      PExpect(TkDim)
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
          PExpect(TkIdent)
          Dim arrLen As Integer = 0
          If PPeekKind() = TkLParen Then
            PAdvance()
            arrLen = g_tokens(p_pos).intVal
            PExpect(TkIntLit)
            PExpect(TkRParen)
            If arrLen <= 0 Then
              arrLen = -2
            End If
          End If
          faLen(itemCnt) = arrLen
          itemCnt = itemCnt + 1
          If PPeekKind() = TkComma Then
            PAdvance()
            PSkipNewlines()
          Else
            moreItems = False
          End If
        End While
        PExpect(TkAs)
        Dim tk As Integer = 0
        Dim tik As Integer = 0
        Dim tnPos As Integer = 0
        Dim tnLen As Integer = 0
        PParseTypeSpec(ByRef tk, ByRef tik, ByRef tnPos, ByRef tnLen)
        For fi = 0 To itemCnt - 1 Do
          Dim fd As Integer = NewNode(NdFieldDecl)
          With g_nodes(fd)
            .intVal = fnPos(fi)
            .lenVal = fnLen(fi)
            .tyKind = tk
            .tyInnerKind = tik
            .tyArrLen = faLen(fi)
            .tyNamePos = tnPos
            .tyNameLen = tnLen
          End With
          fBuf(fCount) = fd
          fCount = fCount + 1
        End For
        If PPeekKind() = TkComma Then
          PAdvance()
          PSkipNewlines()
        Else
          moreClauses = False
        End If
      End While
      PSkipNewlines()
    End While
    PExpect(TkEnd)
    PExpect(TkStructure)
    Dim sd As Integer = NewNode(NdStructDef)
    With g_nodes(sd)
      .op = vis
      .modId = defModId
      .intVal = sPos
      .lenVal = sLen
      .firstChild = g_kidCount
      .childCount = fCount
    End With
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
  If k = TkEnum Then
    PAdvance()
    Dim ePos As Integer = g_tokens(p_pos).pos
    Dim eLen As Integer = g_tokens(p_pos).len
    PExpect(TkIdent)
    PSkipNewlines()
    Dim mBuf(128) As Integer
    Dim mCount As Integer = 0
    While (PPeekKind() <> TkEnd) And (PPeekKind() <> TkEof) Do
      Dim mPos As Integer = g_tokens(p_pos).pos
      Dim mLen As Integer = g_tokens(p_pos).len
      PExpect(TkIdent)
      PExpect(TkEq)
      Dim sign As Integer = 1
      If PPeekKind() = TkMinus Then
        sign = -1
        PAdvance()
      End If
      Dim mVal As Integer = sign * g_tokens(p_pos).intVal
      PExpect(TkIntLit)
      PSkipNewlines()
      Dim md As Integer = NewNode(NdEnumMember)
      g_nodes(md).intVal = mPos
      g_nodes(md).lenVal = mLen
      g_nodes(md).op = mVal
      mBuf(mCount) = md
      mCount = mCount + 1
    End While
    PExpect(TkEnd)
    PExpect(TkEnum)
    Dim ed As Integer = NewNode(NdEnumDef)
    With g_nodes(ed)
      .op = vis
      .modId = defModId
      .intVal = ePos
      .lenVal = eLen
      .firstChild = g_kidCount
      .childCount = mCount
    End With
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
  If k = TkVolatile Then
    isVol = 1
    PAdvance()
    k = PPeekKind()
  End If
  If k = TkStatic Then
    PAdvance()
    PParseVarDeclGroup(NdGlobalVar, vis, isVol, defModId, defBuf, ByRef defCount)
    Return
  End If

  ' Declare Function/Sub name(params [, ...]) [As T]
  If k = TkDeclare Then
    PAdvance()
    Dim isSub As Integer = 0
    If PPeekKind() = TkSub Then
      isSub = 1
      PAdvance()
    Else
      PExpect(TkFunction)
    End If
    Dim nPos As Integer = g_tokens(p_pos).pos
    Dim nLen As Integer = g_tokens(p_pos).len
    PExpect(TkIdent)
    PExpect(TkLParen)
    Dim paramBuf(32) As Integer
    Dim paramCount As Integer = 0
    Dim isVar As Integer = 0
    PParseParamList(1, paramBuf, ByRef paramCount, ByRef isVar)
    PExpect(TkRParen)
    Dim rKind As Integer = TyNothing
    Dim rInnerKind As Integer = TyNothing
    Dim rnPos As Integer = 0
    Dim rnLen As Integer = 0
    If isSub = 0 Then
      PExpect(TkAs)
      PParseTypeSpec(ByRef rKind, ByRef rInnerKind, ByRef rnPos, ByRef rnLen)
    End If
    Dim ex As Integer = NewNode(NdExternDecl)
    With g_nodes(ex)
      .op = isVar
      .intVal = nPos
      .lenVal = nLen
      .tyKind = rKind
      .tyInnerKind = rInnerKind
      .tyNamePos = rnPos
      .tyNameLen = rnLen
      .extraStart = g_kidCount
      .extraCount = paramCount
    End With
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
  If k = TkTest Then
    PAdvance()
    Dim tPos As Integer = g_tokens(p_pos).pos
    Dim tLen As Integer = g_tokens(p_pos).len
    PExpect(TkStrLit)
    Dim bodyStart As Integer = 0
    Dim bodyCount As Integer = 0
    PParseBlock(0, ByRef bodyStart, ByRef bodyCount)
    PExpect(TkEnd)
    PExpect(TkTest)
    Dim td As Integer = NewNode(NdTestDef)
    With g_nodes(td)
      .modId = defModId
      .intVal = tPos
      .lenVal = tLen
      .firstChild = bodyStart
      .childCount = bodyCount
    End With
    defBuf(defCount) = td
    defCount = defCount + 1
    Return
  End If

  ' [Public | Private] Operator <op>(params) As RetType ... End Operator
  If k = TkOperator Then
    PAdvance()
    Dim opTok As Integer = PPeekKind()
    If Not PIsOverloadableOp(opTok) Then
      Printf(MSG_PARSE_EXPECT, TkPlus, opTok, g_tokens(p_pos).line)
      p_errors = p_errors + 1
    End If
    PAdvance()
    PExpect(TkLParen)
    Dim opParamBuf(32) As Integer
    Dim opParamCount As Integer = 0
    Dim opDummyVar As Integer = 0
    PParseParamList(0, opParamBuf, ByRef opParamCount, ByRef opDummyVar)
    PExpect(TkRParen)
    PExpect(TkAs)
    Dim opRetKind = TyNothing, opRetInner = TyNothing, opRetNPos = 0, opRetNLen = 0 As Integer
    PParseTypeSpec(ByRef opRetKind, ByRef opRetInner, ByRef opRetNPos, ByRef opRetNLen)
    Dim opBodyStart = 0, opBodyCount = 0 As Integer
    PParseBlock(0, ByRef opBodyStart, ByRef opBodyCount)
    PExpect(TkEnd)
    PExpect(TkOperator)
    Dim opId As Integer = NewNode(NdOpDef)
    With g_nodes(opId)
      .op = vis
      .intVal = opTok
      .modId = defModId
      .tyKind = opRetKind
      .tyInnerKind = opRetInner
      .tyNamePos = opRetNPos
      .tyNameLen = opRetNLen
      .firstChild = opBodyStart
      .childCount = opBodyCount
      .extraStart = g_kidCount
      .extraCount = opParamCount
    End With
    For oi = 0 To opParamCount - 1 Do
      PushKid(opParamBuf(oi))
    End For
    defBuf(defCount) = opId
    defCount = defCount + 1
    Return
  End If

  ' [Async] Function / Sub definition
  Dim isAsync As Integer = 0
  If k = TkAsync Then
    isAsync = 1
    PAdvance()
    k = PPeekKind()
  End If
  Dim isSub As Integer = 0
  If k = TkSub Then
    isSub = 1
    PAdvance()
  Else
    PExpect(TkFunction)
  End If
  Dim namePos As Integer = g_tokens(p_pos).pos
  Dim nameLen As Integer = g_tokens(p_pos).len
  PExpect(TkIdent)
  PExpect(TkLParen)
  Dim paramBuf(32) As Integer
  Dim paramCount As Integer = 0
  Dim dummyVar As Integer = 0
  PParseParamList(0, paramBuf, ByRef paramCount, ByRef dummyVar)
  PExpect(TkRParen)

  Dim retKind As Integer = TyNothing
  Dim retInnerKind As Integer = TyNothing
  Dim retNamePos As Integer = 0
  Dim retNameLen As Integer = 0
  If isSub = 0 Then
    PExpect(TkAs)
    PParseTypeSpec(ByRef retKind, ByRef retInnerKind, ByRef retNamePos, ByRef retNameLen)
  End If

  Dim bodyStart As Integer = 0
  Dim bodyCount As Integer = 0
  PParseBlock(0, ByRef bodyStart, ByRef bodyCount)

  PExpect(TkEnd)
  If isSub = 1 Then
    PExpect(TkSub)
  Else
    PExpect(TkFunction)
  End If

  Dim fnId As Integer = NewNode(NdFuncDef)
  With g_nodes(fnId)
    .op = vis
    .flags = isAsync
    .modId = defModId
    .intVal = namePos
    .lenVal = nameLen
    .tyKind = retKind
    .tyInnerKind = retInnerKind
    .tyNamePos = retNamePos
    .tyNameLen = retNameLen
    .firstChild = bodyStart
    .childCount = bodyCount
    .extraStart = g_kidCount
    .extraCount = paramCount
  End With
  For i = 0 To paramCount - 1 Do
    PushKid(paramBuf(i))
  End For
  defBuf(defCount) = fnId
  defCount = defCount + 1
End Sub

Public Function ParseProgram() As Integer
  p_pos = 0
  p_errors = 0
  p_withDepth = 0
  Dim defBuf(2048) As Integer
  Dim defCount As Integer = 0
  PSkipNewlines()
  While PPeekKind() <> TkEof Do
    PParseDefInto(defBuf, ByRef defCount)
    PSkipNewlines()
  End While
  Dim progId As Integer = NewNode(NdProgram)
  g_nodes(progId).op = p_errors
  g_nodes(progId).firstChild = g_kidCount
  g_nodes(progId).childCount = defCount
  For i = 0 To defCount - 1 Do
    PushKid(defBuf(i))
  End For
  Return progId
End Function
