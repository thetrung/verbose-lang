' Explicit call-site ByRef example (structs, primitives, and nested fields)
Structure Point
  Dim x As Integer
  Dim y As Integer
End Structure

Sub Increment(ByRef n As Integer)
  n = n + 1
End Sub

Sub MovePoint(ByRef pt As Point, ByVal dx As Integer, ByVal dy As Integer)
  pt.x = pt.x + dx
  pt.y = pt.y + dy
End Sub

Public Function main() As Integer
  Dim count As Integer = 10
  Increment(ByRef count)
  printf("count = %d\n", count)

  Dim p As Point(3, 4)
  MovePoint(ByRef p, 7, 6)
  Increment(ByRef p.x)
  printf("point = (%d, %d)\n", p.x, p.y)
  Return 0
End Function
