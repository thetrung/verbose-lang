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

Public Structure Mix
  Public vec As Vector
  Public node As Integer
End Structure

Function main() As Integer
  Dim vec1 As Vector(1.0, 2.000, 9999999)
  printf("vec1 = (%f -- %f -- %lld)\n", vec1.x, vec1.y, vec1.z)
  
  Dim nodeType As Integer = NodeType.Literal
  printf("nodeType = %d\n", nodeType)

  Dim mix As Mix(vec1, 1)
  mix.vec = Vector(1.0, 1.0, 999)
  ' mix.vec = vec1 
  mix.node = NodeType.Literal
  printf("mix:\n mix.vec = @%d -- vec1 = @%d\n", mix.vec, vec1)
  printf(" mix.vec.x = %f\n", mix.vec.x)
  printf(" mix.vec.y = %f\n", mix.vec.y)
  printf(" mix.vec.z = %lld\n", mix.vec.z)
  printf(" node = %d\n",mix.node)
  Return 0
End Function
