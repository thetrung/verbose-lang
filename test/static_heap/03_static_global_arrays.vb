Static g_zero(4) As Integer
Static g_primes(4) As Integer = {2, 3, 5, 7}

Private Sub AddOne(ByRef arr() As Integer, n As Integer)
  For i = 0 To n - 1
    arr(i) = arr(i) + 1
  End For
End Sub

Public Function main() As Integer
  g_zero(2) = 99
  AddOne(ByRef g_primes, Len(g_primes))
  Printf("zero=%d,%d primes=%d,%d,%d,%d len=%d\n", g_zero(0), g_zero(2), g_primes(0), g_primes(1), g_primes(2), g_primes(3), Len(g_primes))
  Return 0
End Function
