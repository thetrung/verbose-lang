Include "shared.vb"

Public Function main() As Integer
  BumpShared(15)
  printf("incdir=%d\n", g_shared_counter)
  Return 0
End Function
