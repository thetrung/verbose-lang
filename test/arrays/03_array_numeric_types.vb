Public Function main() As Integer
  Dim longs(3) As Long = {1000000, 2000000, 3000000}
  Dim floats(2) As Single = {1.5, 3.25}
  Dim doubles(2) As Double = {2.5, 4.75}
  Dim bytes(3) As Byte = {65, 66, 67}
  Dim flags(2) As Boolean = {True, False}
  printf("l=%ld f=%.2f d=%.2f b=%c%c%c fl=%d,%d\n", longs(2), floats(1), doubles(1), bytes(0), bytes(1), bytes(2), flags(0), flags(1))
  Return 0
End Function
