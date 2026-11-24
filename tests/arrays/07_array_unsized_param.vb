Private Function Sum(arr() As Integer, n As Integer) As Integer
  Dim acc As Integer = 0
  For i = 0 To n - 1 Do
    acc = acc + arr(i)
  End For
  Return acc
End Function

Private Sub Scale(ByRef arr() As Integer, n As Integer, k As Integer)
  For i = 0 To n - 1 Do
    arr(i) = arr(i) * k
  End For
End Sub

Public Function main() As Integer
  Dim a() As Integer = (1, 2, 3)
  Dim b() As Integer = (10, 20, 30, 40)
  Scale(ByRef a, Len(a), 5)
  Scale(ByRef b, Len(b), 2)
  Printf("sumA=%d sumB=%d\n", Sum(a, Len(a)), Sum(b, Len(b)))
  Return 0
End Function
