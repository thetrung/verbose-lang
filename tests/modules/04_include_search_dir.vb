Include "shared.vb"

Public Function main() As Integer
  BumpShared(15)
  Printf("incdir=%d\n", g_shared_counter)
  Return 0
End Function
