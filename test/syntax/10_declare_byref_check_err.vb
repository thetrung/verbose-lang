Declare Sub external_inc(ByRef n As Integer)

Public Function main() As Integer
  Dim a As Integer = 10
  external_inc(a)
  Return 0
End Function
