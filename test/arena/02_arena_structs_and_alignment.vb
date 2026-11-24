Structure Node
  Dim id As Integer
  Dim weight As Double
End Structure

Private Sub BumpNode(ByRef n As Node)
  n.id = n.id + 100
End Sub

Public Function main() As Integer
  Dim a As Arena = ArenaCreate(512)
  Dim b As Pointer = ArenaAllocBytes(ByRef a, 1)
  b[0] = 77
  Dim afterByte As Long = ArenaUsed(ByRef a)

  Dim nodes[] As Node = ArenaAlloc(ByRef a, Node, 3)
  nodes[1] = Node With { id = 5, weight = 2.5 }
  BumpNode(ByRef nodes[1])
  printf("align=%ld b0=%d node1=%d,%.1f used=%ld\n", afterByte, b[0], nodes[1].id, nodes[1].weight, ArenaUsed(ByRef a))
  ArenaDestroy(ByRef a)
  Return 0
End Function
