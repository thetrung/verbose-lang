Structure Box
  Dim vals(3) As Integer
End Structure

Private Sub IncInt(ByRef x As Integer)
  x = x + 10
End Sub

Public Function main() As Integer
  Dim nums() As Integer = (1, 2, 3)
  Dim b As Box
  b.vals(2) = 5
  IncInt(ByRef nums(1))
  IncInt(ByRef b.vals(2))
  Printf("nums1=%d b2=%d\n", nums(1), b.vals(2))
  Return 0
End Function
