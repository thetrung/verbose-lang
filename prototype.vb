Public Structure Vector
  Public x As Single
  Public y As Double
  Public z As Long
End Structure

Public Enum NodeType
  Literal = 1
  BinaryOp = 2
  FunctionCall = 3
End Enum

Function main() As Integer
  Dim vec1 As Vector(1.0, 2.000, 9999999)
  printf("vec1 = (%f -- %f -- %lld)\n", vec1.x, vec1.y, vec1.z)
  
  Dim nodeType As Integer = NodeType.Literal
  printf("nodeType = %d\n", nodeType)

  Return 0
End Function
