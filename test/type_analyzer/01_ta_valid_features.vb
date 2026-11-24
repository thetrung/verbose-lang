Structure Point
  Dim x As Single
  Dim y As Double
  Dim z As Long
End Structure

Static g_val As Integer = 100

Private Function CheckBool(flag As Boolean) As Boolean
  Return Not flag
End Function

Public Function main() As Integer
  Dim uninit_i As Integer
  Dim uninit_p As Point
  Dim p As Point(1.5, 2.5, 9999)
  Dim g_val As Integer = 7
  Dim ok As Boolean = (g_val < 10)
  Dim flipped As Boolean = CheckBool(ok)
  Dim b As Byte = 65
  printf("ui=%d up=%f p=%f,%f,%ld gv=%d ok=%d fl=%d b=%d\n", uninit_i, uninit_p.x, p.x, p.y, p.z, g_val, ok, flipped, b)
  Return 0
End Function
