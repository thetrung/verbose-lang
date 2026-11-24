' VerBose Bootstrap Compiler (vbc) - Arena-allocated AST Pool

Public Enum NodeKind
  NdIntLit = 1
  NdStrLit = 2
  NdVar = 3
  NdBinOp = 4
  NdNeg = 5
  NdCall = 6
  NdDim = 10
  NdAssign = 11
  NdExprStmt = 12
  NdReturn = 13
  NdIf = 14
  NdWhile = 15
  NdParam = 20
  NdFunc = 21
  NdProgram = 22
End Enum

Public Structure AstNode
  Dim kind As Integer
  Dim op As Integer
  Dim intVal As Integer
  Dim lenVal As Integer
  Dim left As Integer
  Dim right As Integer
  Dim firstChild As Integer
  Dim childCount As Integer
  Dim extraStart As Integer
  Dim extraCount As Integer
End Structure

Public Static g_nodes[] As AstNode
Public Static g_nodeCount As Integer
Public Static g_nodeCap As Integer
Public Static g_kids[] As Integer
Public Static g_kidCount As Integer
Public Static g_kidCap As Integer

Public Sub AstInit(ByRef arena As Arena)
  g_nodeCap = 2048
  g_nodeCount = 0
  g_nodes = ArenaAlloc(ByRef arena, AstNode, g_nodeCap)
  g_kidCap = 4096
  g_kidCount = 0
  g_kids = ArenaAlloc(ByRef arena, Integer, g_kidCap)
End Sub

Public Function NewNode(kind As Integer) As Integer
  Dim id As Integer = g_nodeCount
  g_nodes[id] = AstNode With {
    kind = kind,
    op = 0,
    intVal = 0,
    lenVal = 0,
    left = -1,
    right = -1,
    firstChild = 0,
    childCount = 0,
    extraStart = 0,
    extraCount = 0
  }
  g_nodeCount = g_nodeCount + 1
  Return id
End Function

Public Function PushKid(nodeId As Integer) As Integer
  Dim idx As Integer = g_kidCount
  g_kids[idx] = nodeId
  g_kidCount = g_kidCount + 1
  Return idx
End Function
