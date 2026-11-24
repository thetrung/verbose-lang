Static g_pool() As Integer

Private Sub InitPool()
  g_pool = HeapAlloc(Integer, 4)
  g_pool(0) = 11
  g_pool(3) = 44
End Sub

Public Function main() As Integer
  InitPool()
  Dim raw As Pointer = HeapAllocBytes(8)
  raw(0) = 79
  raw(1) = 75
  raw(2) = 0
  Printf("pool=%d,%d raw=%s\n", g_pool(0), g_pool(3), raw)
  HeapFree(raw)
  HeapFree(g_pool)
  Return 0
End Function
