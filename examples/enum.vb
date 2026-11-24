Enum NodeType
  Literal = 1
  BinaryOp = 2
  FunctionCall = 3
End Enum

Public Function main() As Integer
  Dim nodeType As Integer = NodeType.Literal
  Printf("nodeType = %d\n", nodeType)
  Return 0
End Function
