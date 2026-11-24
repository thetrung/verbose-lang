Public Function main() As Integer
  Dim a As Arena = ArenaCreate(256)
  Dim perm[] As Integer = ArenaAlloc(ByRef a, Integer, 2)
  perm[0] = 7
  perm[1] = 8

  Dim mark As Long = ArenaMark(ByRef a)
  Dim scratch[] As Integer = ArenaAlloc(ByRef a, Integer, 16)
  scratch[0] = 999
  Dim peak As Long = ArenaUsed(ByRef a)

  ArenaRewind(ByRef a, mark)
  Dim rewound As Long = ArenaUsed(ByRef a)
  Dim fresh[] As Integer = ArenaAlloc(ByRef a, Integer, 2)
  printf("mark=%ld peak=%ld rewound=%ld perm=%d,%d fresh0=%d\n", mark, peak, rewound, perm[0], perm[1], fresh[0])
  ArenaDestroy(ByRef a)
  Return 0
End Function
