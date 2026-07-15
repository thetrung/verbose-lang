Structure Vector
  Dim x As Single
  Dim y As Double
  Dim z As Long
End Structure

Public Function main() As Integer
  Dim vec1 As Vector(1.0, 2.000, 9999999)
  printf("vec1 = (%f -- %f -- %lld)\n", vec1.x, vec1.y, vec1.z)
  Return 0
End Function
