Structure Config
  Dim port As Integer
  Dim retries As Integer
End Structure

Static g_zero As Config
Static g_cfg As Config With ( port = 8080, retries = 3 )

Private Sub BumpPort(ByRef c As Config)
  c.port = c.port + 1
End Sub

Public Function main() As Integer
  BumpPort(ByRef g_cfg)
  Printf("zero=%d,%d cfg=%d,%d\n", g_zero.port, g_zero.retries, g_cfg.port, g_cfg.retries)
  Return 0
End Function
