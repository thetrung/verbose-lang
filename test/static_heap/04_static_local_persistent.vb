Private Function NextVal() As Integer
  Static count As Integer = 0
  Static hist[3] As Integer
  hist[count] = (count + 1) * 10
  Dim out As Integer = hist[count]
  count = count + 1
  Return out
End Function

Public Function main() As Integer
  Dim a As Integer = NextVal()
  Dim b As Integer = NextVal()
  Dim c As Integer = NextVal()
  printf("seq=%d,%d,%d\n", a, b, c)
  Return 0
End Function
