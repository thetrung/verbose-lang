Function BadRet() As Integer
  Dim b As Boolean = True
  Return b
End Function

Public Function main() As Integer
  Return BadRet()
End Function
