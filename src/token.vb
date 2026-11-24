' VerBose Self-Hosting Compiler (verbose) - Tokens & Type Tags

Public Enum TokenKind
  TkEof = 0
  TkNewline = 1
  TkIdent = 2
  TkIntLit = 3
  TkStrLit = 4
  TkFloatLit = 5

  ' Keywords
  TkInclude = 10
  TkPublic = 11
  TkPrivate = 12
  TkDeclare = 13
  TkStructure = 14
  TkEnum = 15
  TkStatic = 16
  TkFunction = 17
  TkSub = 18
  TkEnd = 19
  TkDim = 20
  TkAs = 21
  TkByVal = 22
  TkByRef = 23
  TkWith = 24
  TkReturn = 25

  ' Types
  TkInteger = 30
  TkByte = 31
  TkShort = 32
  TkLong = 33
  TkBoolean = 34
  TkPointer = 35
  TkArena = 36
  TkNothing = 37
  TkSingle = 38
  TkDouble = 39

  ' Literals & Builtin keywords
  TkTrue = 40
  TkFalse = 41
  TkSizeOf = 42
  TkArenaAlloc = 43
  TkHeapAlloc = 44
  TkHeapRealloc = 45

  ' Control flow
  TkIf = 50
  TkThen = 51
  TkElse = 52
  TkWhile = 53
  TkDo = 54
  TkFor = 55
  TkTo = 56
  TkSelect = 57
  TkCase = 58

  ' Word operators
  TkAnd = 60
  TkOr = 61
  TkNot = 62
  TkMod = 63
  TkXor = 64
  TkShl = 65
  TkShr = 66

  ' Punctuation & symbols
  TkLParen = 70
  TkRParen = 71
  TkLBrace = 72
  TkRBrace = 73
  TkComma = 74
  TkDot = 75
  TkEllipsis = 76
  TkEq = 77
  TkNeq = 78
  TkLt = 79
  TkGt = 80
  TkLe = 81
  TkGe = 82
  TkPlus = 83
  TkMinus = 84
  TkStar = 85
  TkSlash = 86
End Enum

Public Structure Token
  Dim kind As Integer
  Dim pos As Integer
  Dim len As Integer
  Dim intVal As Integer
  Dim line As Integer
  Dim modId As Integer
End Structure
