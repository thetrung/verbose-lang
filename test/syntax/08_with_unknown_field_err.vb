Structure Point
  Dim x As Integer
  Dim y As Integer
End Structure

Public Function main() As Integer
  Dim p As Point With { x = 1, nonexistent = 2 }
  Return 0
End Function
