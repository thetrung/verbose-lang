' Test 02: Field-level mutation on a ByRef struct parameter
Structure Vector
  Dim x As Single
  Dim y As Double
  Dim z As Long
End Structure

Sub MutateFields(ByRef v As Vector)
  v.x = 9.5
  v.y = 12.75
  v.z = 777
End Sub

Public Function main() As Integer
  Dim vec1 As Vector(1.0, 2.0, 3)
  MutateFields(vec1)
  printf("x=%.2f y=%.2f z=%ld\n", vec1.x, vec1.y, vec1.z)
  Return 0
End Function
