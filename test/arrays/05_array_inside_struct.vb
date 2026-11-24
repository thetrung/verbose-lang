Structure Ring
  Dim data(4) As Integer
  Dim count As Integer
End Structure

Private Sub PushAt(ByRef r As Ring, idx As Integer, v As Integer)
  r.data(idx) = v
  r.count = r.count + 1
End Sub

Public Function main() As Integer
  Dim r As Ring
  PushAt(ByRef r, 0, 11)
  PushAt(ByRef r, 3, 44)
  Printf("len=%d count=%d vals=%d,%d,%d\n", Len(r.data), r.count, r.data(0), r.data(1), r.data(3))
  Return 0
End Function
