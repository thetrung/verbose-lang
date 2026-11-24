' Test 11: Deep nested field write (b.vec.z = 999) inside a ByRef Sub
Structure Vector
  Dim x As Single
  Dim y As Double
  Dim z As Long
End Structure

Structure Box
  Dim vec As Vector
  Dim tag As Integer
End Structure

Sub MutateDeep(ByRef b As Box)
  b.vec.z = 999
End Sub

Public Function main() As Integer
  Dim v0 As Vector(1.0, 2.0, 3)
  Dim box As Box(v0, 7)
  MutateDeep(ByRef box)
  printf("z=%ld\n", box.vec.z)
  Return 0
End Function
