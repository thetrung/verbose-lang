Public Function main() As Integer
  Dim a[5] As Integer = [10, 20, 30]
  Dim b[] As Integer = [1, 2, 3, 4]
  Dim c[4] As Integer = b
  c[0] = 999
  printf("a=%d,%d,%d lenA=%d\n", a[0], a[2], a[4], Len(a))
  printf("b0=%d c0=%d lenB=%d\n", b[0], c[0], Len(b))
  Return 0
End Function
