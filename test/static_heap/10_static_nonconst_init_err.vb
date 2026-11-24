Private Function MakeNum() As Integer
  Return 5
End Function

Static bad As Integer = MakeNum()

Public Function main() As Integer
  Return 0
End Function
