Public Function main() As Integer
  Dim a As Arena = ArenaCreate(128)
  Dim first() As Integer = ArenaAlloc(ByRef a, Integer, 4)
  first(0) = 123
  first(1) = 456

  ArenaReset(ByRef a)
  Dim usedAfterReset As Long = ArenaUsed(ByRef a)
  Dim second() As Integer = ArenaAlloc(ByRef a, Integer, 4)
  Printf("reset_used=%ld zeroed=%d,%d\n", usedAfterReset, second(0), second(1))
  ArenaDestroy(ByRef a)
  Return 0
End Function
