' VerBose Self-Hosting Compiler (verbose) - Arena-Allocated AST & Type Metadata

Public Enum TypeKind
  TyNothing = 0
  TyInt = 1
  TyByte = 2
  TyShort = 3
  TyLong = 4
  TyBoolean = 5
  TyPointer = 6
  TyArena = 7
  TyStruct = 8
  TyEnum = 9
  TySingle = 10
  TyDouble = 11
  TyTask = 12
  TyUnion = 13
End Enum

Public Structure TypeInfo
  Dim kind, typeIdx, arrLen, namePos, nameLen, innerKind As Integer
End Structure

Public Enum NodeKind
  ' Expressions
  NdIntLit = 1
  NdBoolLit = 2
  NdStrLit = 3
  NdVar = 4
  NdBinOp = 5
  NdNeg = 6
  NdNot = 7
  NdCall = 8
  NdFieldAccess = 9
  NdIndexAccess = 10
  NdStructInit = 11
  NdArrayLit = 12
  NdSizeOf = 13
  NdArenaAlloc = 14
  NdFieldInit = 15
  NdArg = 16
  NdFloatLit = 17
  NdHeapAlloc = 18
  NdHeapRealloc = 19

  ' Statements
  NdDim = 20
  NdAssign = 21
  NdFieldAssign = 22
  NdIndexAssign = 23
  NdExprStmt = 24
  NdReturn = 25
  NdIf = 26
  NdWhile = 27
  NdFor = 28
  NdStaticDim = 31
  NdAssert = 32

  ' Definitions
  NdParam = 40
  NdFieldDecl = 41
  NdEnumMember = 42
  NdStructDef = 43
  NdEnumDef = 44
  NdGlobalVar = 45
  NdExternDecl = 46
  NdFuncDef = 47
  NdProgram = 48
  NdTestDef = 49
  NdVolatileLoad = 50
  NdVolatileStore = 51
  NdAddressOf = 52
  NdPtrRead = 53
  NdPtrWrite = 54
  NdPtrAdd = 55
  NdLabel = 56
  NdGoTo = 57
  NdAwait = 58
  NdStrInterp = 59
  NdMatch = 60
  NdMatchArm = 61
  NdWith = 62
  NdWithTarget = 63
  NdBreak = 64
  NdContinue = 65
  NdOpDef = 66
  NdCast = 67
  NdTypeRef = 68
  NdUnionDef = 69
  NdUnionVariant = 70
  NdUnionInit = 71
End Enum

Public Structure AstNode
  Dim kind, op, intVal, lenVal As Integer
  Dim longVal As Long
  Dim left, right, third, firstChild, childCount, extraStart, extraCount As Integer
  Dim tyKind, tyArrLen, tyNamePos, tyNameLen, tyInnerKind, modId, flags As Integer
  Dim genStart, genCount, tyArgStart, tyArgCount As Integer
End Structure

Public Static g_nodes() As AstNode, g_kids() As Integer
Public Static g_nodeCount, g_nodeCap, g_kidCount, g_kidCap As Integer

Public Sub AstInit(ByRef arena As Arena)
  g_nodeCap = 131072
  g_nodeCount = 0
  g_nodes = ArenaAlloc(ByRef arena, AstNode, g_nodeCap)
  g_kidCap = 262144
  g_kidCount = 0
  g_kids = ArenaAlloc(ByRef arena, Integer, g_kidCap)
End Sub

Public Function NewNode(kind As Integer) As Integer
  Dim id As Integer = g_nodeCount
  g_nodes(id) = AstNode With (
    kind = kind,
    op = 0,
    intVal = 0,
    lenVal = 0,
    longVal = 0,
    left = -1,
    right = -1,
    third = -1,
    firstChild = 0,
    childCount = 0,
    extraStart = 0,
    extraCount = 0,
    tyKind = TyNothing,
    tyArrLen = 0,
    tyNamePos = 0,
    tyNameLen = 0,
    genStart = 0,
    genCount = 0,
    tyArgStart = 0,
    tyArgCount = 0
  )
  g_nodeCount = g_nodeCount + 1
  Return id
End Function

Public Function PushKid(nodeId As Integer) As Integer
  Dim idx As Integer = g_kidCount
  g_kids(idx) = nodeId
  g_kidCount = g_kidCount + 1
  Return idx
End Function
