Public Structure Vector
  Public x As Single
  Public y As Double
End Structure

Function main() As Integer
  Dim vec1 As Vector(1.0, 2.000)
  printf("vec1 = (%f -- %f)\n", vec1.x, vec1.y)
  Return 0
End Function
