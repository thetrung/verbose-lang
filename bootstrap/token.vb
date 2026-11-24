' VerBose Bootstrap Compiler (vbc) - Token definitions

Public Enum TokenKind
  TkEof = 0
  TkNewline = 1
  TkIdent = 2
  TkIntLit = 3
  TkStrLit = 4
  TkPublic = 10
  TkFunction = 11
  TkSub = 12
  TkEnd = 13
  TkDim = 14
  TkAs = 15
  TkInteger = 16
  TkReturn = 17
  TkIf = 18
  TkThen = 19
  TkElse = 20
  TkWhile = 21
  TkDo = 22
  TkLParen = 30
  TkRParen = 31
  TkComma = 32
  TkEq = 33
  TkNeq = 34
  TkLt = 35
  TkGt = 36
  TkLe = 37
  TkGe = 38
  TkPlus = 39
  TkMinus = 40
  TkStar = 41
  TkSlash = 42
End Enum

Public Structure Token
  Dim kind As Integer
  Dim pos As Integer
  Dim len As Integer
  Dim intVal As Integer
  Dim line As Integer
End Structure
