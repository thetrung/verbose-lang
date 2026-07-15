Structure Vector
  Dim x As Single
  Dim y As Double
  Dim z As Long
End Structure

Enum NodeType
  Literal = 1
  BinaryOp = 2
  FunctionCall = 3
End Enum

Structure Mix
  Dim vec As Vector
  Dim node As Integer
End Structure

Function Square(number As Long) As Long
  Dim result As Long = number * number
  Return result
End Function

Function main() As Integer
  Dim vec1 As Vector(1.0, 2.000, 9999999)
  printf("vec1@%lu :\n %f -- %f -- %ld)\n", vec1, vec1.x, vec1.y, vec1.z)
  
  Dim nodeType As Integer = NodeType.Literal
  printf("nodeType = %d\n", nodeType)

  Dim squared As Long = Square(vec1.z)
  printf("vec1.z ^ 2 = %ld\n", squared)

  Dim mix As Mix(vec1, 1)
  ' mix.vec = Vector(1.0, 1.0, 999)
  mix.node = NodeType.Literal
  printf("mix:\n .vec = @%lu\n", mix.vec)
  printf("    .x single %f\n", mix.vec.x)
  printf("    .y double %f\n", mix.vec.y)
  printf("    .z long   %lld\n", mix.vec.z)
  printf(" .node integer %d\n",mix.node)
  Return 0
End Function
