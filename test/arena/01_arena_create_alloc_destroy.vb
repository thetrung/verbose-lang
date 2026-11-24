Public Function main() As Integer
  Dim a As Arena = ArenaCreate(256)
  Dim nums() As Integer = ArenaAlloc(ByRef a, Integer, 4)
  Printf("zero=%d,%d cap=%ld used=%ld rem=%ld\n", nums(0), nums(3), ArenaCapacity(ByRef a), ArenaUsed(ByRef a), ArenaRemaining(ByRef a))
  nums(0) = 10
  nums(3) = 40
  Printf("vals=%d,%d\n", nums(0), nums(3))

  ArenaDestroy(ByRef a)
  Dim afterDestroy As Pointer = ArenaAllocBytes(ByRef a, 16)
  Printf("destroyed cap=%ld used=%ld null=%d\n", ArenaCapacity(ByRef a), ArenaUsed(ByRef a), IsNull(afterDestroy))
  Return 0
End Function
