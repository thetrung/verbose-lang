Public Function main() As Integer
  Dim a(5) As Integer = (10, 20, 30)
  Dim b() As Integer = (1, 2, 3, 4)
  Dim s1(1) As Integer = (42)
  Dim s2() As Integer = (77)
  Dim c(4) As Integer = b
  c(0) = 999
  Printf("a=%d,%d,%d lenA=%d\n", a(0), a(2), a(4), Len(a))
  Printf("b0=%d c0=%d lenB=%d\n", b(0), c(0), Len(b))
  Printf("s1=%d lenS1=%d s2=%d lenS2=%d\n", s1(0), Len(s1), s2(0), Len(s2))
  Return 0
End Function
