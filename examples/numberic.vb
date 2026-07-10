
Public Structure Vector
  Public x As Single
  Public y As Double
  Public z As Long
End Structure

Function main() As Integer
  Dim vec1 As Vector(1.0, 2.000, 9999999)
  printf("vec1 = (%f -- %f -- %lld)\n", vec1.x, vec1.y, vec1.z)
  Return 0
End Function
