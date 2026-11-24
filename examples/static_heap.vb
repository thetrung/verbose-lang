Structure Vec2
  Dim x As Integer
  Dim y As Integer
End Structure

Public Static g_calls As Integer = 0
Private Static g_items() As Vec2

Private Function NextTicket() As Integer
  Static seq As Integer = 100
  seq = seq + 1
  g_calls = g_calls + 1
  Return seq
End Function

Public Function main() As Integer
  printf("sizeof(Integer)=%d sizeof(Vec2)=%d\n", SizeOf(Integer), SizeOf(Vec2))
  printf("tickets=%d,%d calls=%d\n", NextTicket(), NextTicket(), g_calls)

  g_items = HeapAlloc(Vec2, 2)
  g_items(0) = Vec2 With { x = 10, y = 20 }
  g_items(1) = Vec2 With { x = 30, y = 40 }
  g_items = HeapRealloc(g_items, Vec2, 3)
  g_items(2) = Vec2 With { x = 50, y = 60 }

  printf("heap=(%d,%d) (%d,%d) (%d,%d)\n", g_items(0).x, g_items(0).y, g_items(1).x, g_items(1).y, g_items(2).x, g_items(2).y)
  HeapFree(g_items)
  Return 0
End Function
