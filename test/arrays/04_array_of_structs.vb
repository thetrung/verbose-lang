Structure Point
  Dim x As Integer
  Dim y As Integer
End Structure

Public Function main() As Integer
  Dim pts(3) As Point
  pts(0) = Point With ( x = 10, y = 20 )
  pts(1).x = 30
  pts(1).y = 40
  pts(2) = pts(0)
  pts(2).x = 99
  Printf("p0=%d,%d p1=%d,%d p2=%d,%d\n", pts(0).x, pts(0).y, pts(1).x, pts(1).y, pts(2).x, pts(2).y)
  Return 0
End Function
