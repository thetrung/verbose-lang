Sub BadSub()
  Return 42
End Sub

Public Function main() As Integer
  BadSub()
  Return 0
End Function
