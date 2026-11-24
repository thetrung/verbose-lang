Structure Pair
  Dim a As Integer
  Dim b As Integer
End Structure

Static g_buf(256) As Byte

Public Function main() As Integer
  Dim arena As Arena = ArenaFromBuffer(ByRef g_buf, Len(g_buf))
  Dim pairs() As Pair = ArenaAlloc(ByRef arena, Pair, 3)
  pairs(0) = Pair With ( a = 11, b = 22 )
  pairs(2) = Pair With ( a = 33, b = 44 )
  Printf("cap=%ld used=%ld p0=%d,%d p2=%d,%d\n", ArenaCapacity(ByRef arena), ArenaUsed(ByRef arena), pairs(0).a, pairs(0).b, pairs(2).a, pairs(2).b)
  Return 0
End Function
