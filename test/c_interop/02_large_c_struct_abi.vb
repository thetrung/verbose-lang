Structure BigBox
  Dim a As Long
  Dim b As Long
  Dim c As Long
End Structure

Declare Function sum_big_byval(box As BigBox) As Long
Declare Sub scale_big_byref(ByRef box As BigBox, k As Long)

Public Function main() As Integer
  Dim b As BigBox With ( a = 10, b = 20, c = 30 )
  Dim s1 As Long = sum_big_byval(b)
  scale_big_byref(ByRef b, 3)
  Dim s2 As Long = sum_big_byval(b)
  printf("s1=%ld s2=%ld fields=%ld,%ld,%ld\n", s1, s2, b.a, b.b, b.c)
  Return 0
End Function
