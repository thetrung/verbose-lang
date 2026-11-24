Structure Point
  Dim x As Integer
  Dim y As Integer
End Structure

Private Sub Translate(ByRef p As Point, dx As Integer, dy As Integer)
  p.x = p.x + dx
  p.y = p.y + dy
End Sub

Private Function SumX(pts() As Point, n As Integer) As Integer
  Dim acc As Integer = 0
  For i = 0 To n - 1
    acc = acc + pts(i).x
  End For
  Return acc
End Function

Public Function main() As Integer
  Dim pts() As Point = HeapAlloc(Point, 3)
  pts(0) = Point With ( x = 1, y = 2 )
  pts(1) = Point With ( x = 10, y = 20 )
  pts(2) = Point With ( x = 100, y = 200 )
  Translate(ByRef pts(1), 5, 6)
  printf("p1=%d,%d sumX=%d\n", pts(1).x, pts(1).y, SumX(pts, 3))
  HeapFree(pts)
  Return 0
End Function
