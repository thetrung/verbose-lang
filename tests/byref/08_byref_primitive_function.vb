' Test 08: ByRef on primitive parameters in Sub and Function
Sub SetInt(ByRef x As Integer)
  x = 42
End Sub

Function IncrementInt(ByRef n As Integer) As Integer
  n = n + 1
  Return n
End Function

Public Function main() As Integer
  Dim a As Integer = 1
  Dim b As Integer = 10
  SetInt(ByRef a)
  Dim res As Integer
  res = IncrementInt(ByRef b)
  Printf("a=%d b=%d res=%d\n", a, b, res)
  Return 0
End Function
