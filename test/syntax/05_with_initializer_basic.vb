Structure Vector
  Dim x As Single
  Dim y As Double
  Dim z As Long
End Structure

Public Function main() As Integer
  Dim v1 As Vector With (
    x = 3.14, ' non-exact Single float literal + inline comment
    y = 2.75,
    z = 9999999,
  )
  Dim v2 As Vector With ( z = 7, x = 1.5, y = 4.5 )
  Printf("v1=%.2f,%.2f,%lld v2=%.1f,%.1f,%lld\n", v1.x, v1.y, v1.z, v2.x, v2.y, v2.z)
  Return 0
End Function
