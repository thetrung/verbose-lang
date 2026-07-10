Public Enum NodeType
  Literal = 1
  BinaryOp = 2
  FunctionCall = 3
End Enum

Function main() As Integer
  Dim nodeType As Integer = NodeType.Literal
  printf("nodeType = %d\n", nodeType)
  Return 0
End Function
