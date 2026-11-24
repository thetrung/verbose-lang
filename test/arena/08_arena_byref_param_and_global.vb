Static g_arena As Arena

Private Function AllocAndSum(ByRef a As Arena, v0 As Integer, v1 As Integer) As Integer
  Dim pair() As Integer = ArenaAlloc(ByRef a, Integer, 2)
  pair(0) = v0
  pair(1) = v1
  Return pair(0) + pair(1)
End Function

Public Function main() As Integer
  g_arena = ArenaCreate(128)
  Dim s1 As Integer = AllocAndSum(ByRef g_arena, 10, 20)
  Dim s2 As Integer = AllocAndSum(ByRef g_arena, 30, 40)
  printf("s1=%d s2=%d used=%ld\n", s1, s2, ArenaUsed(ByRef g_arena))
  ArenaDestroy(ByRef g_arena)
  Return 0
End Function
