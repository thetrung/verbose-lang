' Test 16 (expected error): Passing a non-lvalue literal with ByRef at call site
Sub SetInt(ByRef x As Integer)
  x = 42
End Sub

Public Function main() As Integer
  SetInt(ByRef 123)
  Return 0
End Function
