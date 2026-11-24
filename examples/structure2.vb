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

Private Sub UpdateMix(ByRef m As Mix)
  m.vec = Vector With (
    x = 3.5,
    y = 4.25,
    z = 999
  )
  m.node = NodeType.FunctionCall
End Sub

Public Function main() As Integer
  Dim vec1 As Vector With (
    x = 1.0,
    y = 2.000,
    z = 9999999
  )
  Printf("vec1 = (%f -- %f -- %lld)\n", vec1.x, vec1.y, vec1.z)

  Dim nodeType As Integer = NodeType.Literal
  Printf("nodeType = %d\n", nodeType)

  Dim mix As Mix With (
    vec = vec1,
    node = NodeType.Literal
  )
  UpdateMix(ByRef mix)

  Printf("mix:\n mix.vec = @%lu -- vec1 = @%lu\n", mix.vec, vec1)
  Printf(" mix.vec.x = %f\n", mix.vec.x)
  Printf(" mix.vec.y = %f\n", mix.vec.y)
  Printf(" mix.vec.z = %lld\n", mix.vec.z)
  Printf(" node = %d\n", mix.node)
  Return 0
End Function
