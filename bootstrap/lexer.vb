' VerBose Bootstrap Compiler (vbc) - Lexer (Arena-backed)

Include "token.vb"

Public Static g_src As Pointer
Public Static g_srcLen As Integer
Public Static g_tokens() As Token
Public Static g_tokCount As Integer
Public Static g_tokCap As Integer

Private Function IsAlpha(c As Integer) As Boolean
  If (c >= 65) And (c <= 90) Then
    Return True
  End If
  If (c >= 97) And (c <= 122) Then
    Return True
  End If
  If c = 95 Then
    Return True
  End If
  Return False
End Function

Private Function IsDigit(c As Integer) As Boolean
  Return (c >= 48) And (c <= 57)
End Function

Private Function IsAlnum(c As Integer) As Boolean
  Return IsAlpha(c) Or IsDigit(c)
End Function

Public Function SliceEqualsStr(pos As Integer, slen As Integer, lit As Pointer, litLen As Integer) As Boolean
  If slen <> litLen Then
    Return False
  End If
  Dim i As Integer = 0
  While i < slen Do
    Dim a As Integer = g_src(pos + i)
    Dim b As Integer = lit(i)
    If a <> b Then
      Return False
    End If
    i = i + 1
  End While
  Return True
End Function

Public Function SlicesEqual(p1 As Integer, l1 As Integer, p2 As Integer, l2 As Integer) As Boolean
  If l1 <> l2 Then
    Return False
  End If
  Dim i As Integer = 0
  While i < l1 Do
    Dim a As Integer = g_src(p1 + i)
    Dim b As Integer = g_src(p2 + i)
    If a <> b Then
      Return False
    End If
    i = i + 1
  End While
  Return True
End Function

Private Function ClassifyKeyword(pos As Integer, slen As Integer) As Integer
  If SliceEqualsStr(pos, slen, "Public", 6) Then
    Return TokenKind.TkPublic
  End If
  If SliceEqualsStr(pos, slen, "Function", 8) Then
    Return TokenKind.TkFunction
  End If
  If SliceEqualsStr(pos, slen, "Sub", 3) Then
    Return TokenKind.TkSub
  End If
  If SliceEqualsStr(pos, slen, "End", 3) Then
    Return TokenKind.TkEnd
  End If
  If SliceEqualsStr(pos, slen, "Dim", 3) Then
    Return TokenKind.TkDim
  End If
  If SliceEqualsStr(pos, slen, "As", 2) Then
    Return TokenKind.TkAs
  End If
  If SliceEqualsStr(pos, slen, "Integer", 7) Then
    Return TokenKind.TkInteger
  End If
  If SliceEqualsStr(pos, slen, "Return", 6) Then
    Return TokenKind.TkReturn
  End If
  If SliceEqualsStr(pos, slen, "If", 2) Then
    Return TokenKind.TkIf
  End If
  If SliceEqualsStr(pos, slen, "Then", 4) Then
    Return TokenKind.TkThen
  End If
  If SliceEqualsStr(pos, slen, "Else", 4) Then
    Return TokenKind.TkElse
  End If
  If SliceEqualsStr(pos, slen, "While", 5) Then
    Return TokenKind.TkWhile
  End If
  If SliceEqualsStr(pos, slen, "Do", 2) Then
    Return TokenKind.TkDo
  End If
  Return TokenKind.TkIdent
End Function

Private Sub EmitTok(kind As Integer, pos As Integer, slen As Integer, ival As Integer, line As Integer)
  If g_tokCount < g_tokCap Then
    g_tokens(g_tokCount) = Token With {
      kind = kind,
      pos = pos,
      len = slen,
      intVal = ival,
      line = line
    }
    g_tokCount = g_tokCount + 1
  End If
End Sub

Public Function TokenizeAll(ByRef arena As Arena, src As Pointer, srcLen As Integer) As Integer
  g_src = src
  g_srcLen = srcLen
  g_tokCap = 4096
  g_tokCount = 0
  g_tokens = ArenaAlloc(ByRef arena, Token, g_tokCap)

  Dim pos As Integer = 0
  Dim line As Integer = 1

  While pos < srcLen Do
    Dim c As Integer = src(pos)

    ' Skip spaces, tabs, CR
    If (c = 32) Or (c = 9) Or (c = 13) Then
      pos = pos + 1
    Else
      If c = 10 Then
        EmitTok(TokenKind.TkNewline, pos, 1, 0, line)
        pos = pos + 1
        line = line + 1
      Else
        ' Single-quote comment until newline
        If c = 39 Then
          While (pos < srcLen) And (src(pos) <> 10) Do
            pos = pos + 1
          End While
        Else
          ' String literal "..."
          If c = 34 Then
            pos = pos + 1
            Dim sStart As Integer = pos
            While (pos < srcLen) And (src(pos) <> 34) Do
              Dim sc As Integer = src(pos)
              If (sc = 92) And ((pos + 1) < srcLen) Then
                pos = pos + 2
              Else
                pos = pos + 1
              End If
            End While
            Dim sLen As Integer = pos - sStart
            If pos < srcLen Then
              pos = pos + 1
            End If
            EmitTok(TokenKind.TkStrLit, sStart, sLen, 0, line)
          Else
            ' Identifier or keyword
            If IsAlpha(c) Then
              Dim idStart As Integer = pos
              While (pos < srcLen) And IsAlnum(src(pos)) Do
                pos = pos + 1
              End While
              Dim idLen As Integer = pos - idStart
              Dim kw As Integer = ClassifyKeyword(idStart, idLen)
              EmitTok(kw, idStart, idLen, 0, line)
            Else
              ' Integer literal
              If IsDigit(c) Then
                Dim nStart As Integer = pos
                Dim acc As Integer = 0
                While (pos < srcLen) And IsDigit(src(pos)) Do
                  Dim d As Integer = src(pos)
                  acc = acc * 10 + (d - 48)
                  pos = pos + 1
                End While
                EmitTok(TokenKind.TkIntLit, nStart, pos - nStart, acc, line)
              Else
                ' Operators & punctuation
                Dim nextC As Integer = 0
                If (pos + 1) < srcLen Then
                  nextC = src(pos + 1)
                End If
                If (c = 60) And (nextC = 62) Then
                  EmitTok(TokenKind.TkNeq, pos, 2, 0, line)
                  pos = pos + 2
                Else
                  If (c = 60) And (nextC = 61) Then
                    EmitTok(TokenKind.TkLe, pos, 2, 0, line)
                    pos = pos + 2
                  Else
                    If (c = 62) And (nextC = 61) Then
                      EmitTok(TokenKind.TkGe, pos, 2, 0, line)
                      pos = pos + 2
                    Else
                      Select Case c
                        Case 40
                          EmitTok(TokenKind.TkLParen, pos, 1, 0, line)
                        Case 41
                          EmitTok(TokenKind.TkRParen, pos, 1, 0, line)
                        Case 44
                          EmitTok(TokenKind.TkComma, pos, 1, 0, line)
                        Case 61
                          EmitTok(TokenKind.TkEq, pos, 1, 0, line)
                        Case 60
                          EmitTok(TokenKind.TkLt, pos, 1, 0, line)
                        Case 62
                          EmitTok(TokenKind.TkGt, pos, 1, 0, line)
                        Case 43
                          EmitTok(TokenKind.TkPlus, pos, 1, 0, line)
                        Case 45
                          EmitTok(TokenKind.TkMinus, pos, 1, 0, line)
                        Case 42
                          EmitTok(TokenKind.TkStar, pos, 1, 0, line)
                        Case 47
                          EmitTok(TokenKind.TkSlash, pos, 1, 0, line)
                        Case Else
                          printf("vbc lexer error: unexpected char %d at line %d\n", c, line)
                          Return -1
                      End Select
                      pos = pos + 1
                    End If
                  End If
                End If
              End If
            End If
          End If
        End If
      End If
    End If
  End While

  EmitTok(TokenKind.TkEof, pos, 0, 0, line)
  Return g_tokCount
End Function
