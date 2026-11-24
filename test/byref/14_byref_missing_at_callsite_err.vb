' Test 14 (expected error): Calling a ByRef parameter without ByRef at the call site
Sub SetInt(ByRef x As Integer)
  x = 42
End Sub

Public Function main() As Integer
  Dim a As Integer = 1
  SetInt(a)
  Return 0
End Function
