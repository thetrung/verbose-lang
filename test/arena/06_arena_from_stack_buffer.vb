Public Function main() As Integer
  Dim stackBuf(128) As Byte
  Dim arena As Arena = ArenaFromBuffer(ByRef stackBuf, Len(stackBuf))
  Dim s As Pointer = ArenaAllocBytes(ByRef arena, 8)
  s(0) = 65
  s(1) = 82
  s(2) = 0
  Printf("s=%s used=%ld rem=%ld\n", s, ArenaUsed(ByRef arena), ArenaRemaining(ByRef arena))
  Return 0
End Function
