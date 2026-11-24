Private Structure Point
  Dim x As Integer
  Dim y As Integer
End Structure

Private Enum Color
  Red = 10
  Blue = 20
End Enum

Private Function AddXY(p As Point) As Integer
  Return p.x + p.y
End Function

Private Sub BumpX(ByRef p As Point, delta As Integer)
  p.x = p.x + delta
End Sub

Public Function main() As Integer
  Dim pt As Point With ( x = Color.Red, y = Color.Blue )
  BumpX(ByRef pt, 5)
  Dim sum As Integer = AddXY(pt)
  Printf("pt=%d,%d sum=%d\n", pt.x, pt.y, sum)
  Return 0
End Function
