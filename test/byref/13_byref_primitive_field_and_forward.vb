' Test 13: Primitive struct field passed ByRef + forwarded primitive ByRef across functions
Structure Pair
  Dim a As Integer
  Dim b As Integer
End Structure

Sub InnerAdd(ByRef x As Integer)
  x = x + 10
End Sub

Sub OuterAdd(ByRef x As Integer)
  x = x + 5
  InnerAdd(ByRef x)
End Sub

Public Function main() As Integer
  Dim p As Pair(1, 2)
  OuterAdd(ByRef p.a)
  Dim n As Integer = 100
  OuterAdd(ByRef n)
  Printf("p.a=%d p.b=%d n=%d\n", p.a, p.b, n)
  Return 0
End Function
