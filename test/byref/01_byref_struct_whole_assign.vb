' Test 01: Whole-struct reassignment via ByRef Sub
Structure Vector
  Dim x As Single
  Dim y As Double
  Dim z As Long
End Structure

Sub ReplaceVec(ByRef v As Vector)
  Dim replacement As Vector(3.5, 4.25, 100)
  v = replacement
End Sub

Public Function main() As Integer
  Dim vec1 As Vector(1.0, 2.0, 10)
  ReplaceVec(ByRef vec1)
  printf("x=%.2f y=%.2f z=%ld\n", vec1.x, vec1.y, vec1.z)
  Return 0
End Function
