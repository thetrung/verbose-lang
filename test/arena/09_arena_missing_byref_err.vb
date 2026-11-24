Public Function main() As Integer
  Dim a As Arena = ArenaCreate(64)
  Dim nums() As Integer = ArenaAlloc(a, Integer, 4)
  Return 0
End Function
