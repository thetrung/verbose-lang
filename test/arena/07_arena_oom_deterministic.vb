Public Function main() As Integer
  Dim a As Arena = ArenaCreate(32)
  Dim p1 As Pointer = ArenaAllocBytes(ByRef a, 16)
  Dim p2 As Pointer = ArenaAllocBytes(ByRef a, 24)
  Dim usedAfterOom As Long = ArenaUsed(ByRef a)
  Dim p3 As Pointer = ArenaAllocBytes(ByRef a, 16)
  printf("n1=%d n2=%d used=%ld n3=%d final=%ld\n", IsNull(p1), IsNull(p2), usedAfterOom, IsNull(p3), ArenaUsed(ByRef a))
  ArenaDestroy(ByRef a)
  Return 0
End Function
