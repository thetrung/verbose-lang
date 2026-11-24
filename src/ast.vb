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
End Enum

Public Structure TypeInfo
  Dim kind As Integer      ' TypeKind
  Dim typeIdx As Integer   ' struct index or enum index
  Dim arrLen As Integer    ' 0 = scalar, -1 = unsized slice (), >0 = fixed array (N)
  Dim namePos As Integer   ' raw custom type name offset in g_srcPool
  Dim nameLen As Integer
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
  NdSelect = 29
  NdCaseBranch = 30
  NdStaticDim = 31

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
End Enum

Public Structure AstNode
  Dim kind As Integer
  Dim op As Integer         ' operator token, bool/is_public/is_byref/is_variadic flags, str id
  Dim intVal As Integer     ' int literal or namePos in g_srcPool
  Dim lenVal As Integer     ' nameLen / strLen in g_srcPool
  Dim left As Integer       ' primary child node id
  Dim right As Integer      ' secondary child node id
  Dim third As Integer      ' tertiary child node id (e.g. For finish / IndexAssign val)
  Dim firstChild As Integer ' start offset in g_kids()
  Dim childCount As Integer
  Dim extraStart As Integer ' secondary child list start in g_kids()
  Dim extraCount As Integer
  Dim tyKind As Integer     ' TypeKind
  Dim tyArrLen As Integer   ' 0, -1, or N
  Dim tyNamePos As Integer  ' custom type name offset in g_srcPool
  Dim tyNameLen As Integer
  Dim modId As Integer      ' source module id for Private isolation
End Structure

Public Static g_nodes() As AstNode
Public Static g_nodeCount As Integer
Public Static g_nodeCap As Integer
Public Static g_kids() As Integer
Public Static g_kidCount As Integer
Public Static g_kidCap As Integer

Public Sub AstInit(ByRef arena As Arena)
  g_nodeCap = 65536
  g_nodeCount = 0
  g_nodes = ArenaAlloc(ByRef arena, AstNode, g_nodeCap)
  g_kidCap = 131072
  g_kidCount = 0
  g_kids = ArenaAlloc(ByRef arena, Integer, g_kidCap)
End Sub

Public Function NewNode(kind As Integer) As Integer
  Dim id As Integer = g_nodeCount
  g_nodes(id) = AstNode With {
    kind = kind,
    op = 0,
    intVal = 0,
    lenVal = 0,
    left = -1,
    right = -1,
    third = -1,
    firstChild = 0,
    childCount = 0,
    extraStart = 0,
    extraCount = 0,
    tyKind = TypeKind.TyNothing,
    tyArrLen = 0,
    tyNamePos = 0,
    tyNameLen = 0
  }
  g_nodeCount = g_nodeCount + 1
  Return id
End Function

Public Function PushKid(nodeId As Integer) As Integer
  Dim idx As Integer = g_kidCount
  g_kids(idx) = nodeId
  g_kidCount = g_kidCount + 1
  Return idx
End Function
