' Test 15 (expected error): Passing ByRef at call site to a ByVal parameter
Sub ReadOnly(ByVal x As Integer)
  Printf("%d\n", x)
End Sub

Public Function main() As Integer
  Dim a As Integer = 1
  ReadOnly(ByRef a)
  Return 0
End Function
