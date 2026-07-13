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

Function main() As Integer
  Dim vec1 As Vector With {
    x = 1.0, 
    y = 2.000, 
    z = 9999999 
  }
  Call printf("vec1 = (%f -- %f -- %lld)\n", vec1.x, vec1.y, vec1.z)
  
  Dim nodeType As Integer = NodeType.Literal
  Call printf("nodeType = %d\n", nodeType)

  Dim mix As Mix With {
    vec = vec1, 
    node = NodeType.Literal 
  }
  mix.vec = Vector(1.0, 1.0, 999)
  mix.node = NodeType.Literal
  
  Call printf("mix:\n mix.vec = @%d -- vec1 = @%d\n", mix.vec, vec1)
  Call printf(" mix.vec.x = %f\n", mix.vec.x)
  Call printf(" mix.vec.y = %f\n", mix.vec.y)
  Call printf(" mix.vec.z = %lld\n", mix.vec.z)
  Call printf(" node = %d\n",mix.node)
  Return 0
End Function
