Structure Particle
  Dim id As Integer
  Dim x As Double
  Dim y As Double
End Structure

Static g_static_pool(512) As Byte

Private Sub InitParticle(ByRef p As Particle, id As Integer, x As Double, y As Double)
  p.id = id
  p.x = x
  p.y = y
End Sub

Public Function main() As Integer
  ' 1. Heap-backed arena with scoped Mark/Rewind & Reset
  Dim frame As Arena = ArenaCreate(1024)
  Dim parts() As Particle = ArenaAlloc(ByRef frame, Particle, 2)
  InitParticle(ByRef parts(0), 1, 1.5, 2.5)
  InitParticle(ByRef parts(1), 2, 3.5, 4.5)

  Dim mark As Long = ArenaMark(ByRef frame)
  Dim temp() As Integer = ArenaAlloc(ByRef frame, Integer, 16)
  temp(0) = 99
  printf("frame cap=%ld used_with_temp=%ld temp0=%d\n", ArenaCapacity(ByRef frame), ArenaUsed(ByRef frame), temp(0))

  ArenaRewind(ByRef frame, mark)
  printf("after_rewind used=%ld p1=(%d,%.1f,%.1f)\n", ArenaUsed(ByRef frame), parts(1).id, parts(1).x, parts(1).y)

  ArenaReset(ByRef frame)
  ArenaDestroy(ByRef frame)

  ' 2. Zero-malloc baremetal-safe arena backed by a Static byte buffer
  Dim embedded As Arena = ArenaFromBuffer(ByRef g_static_pool, Len(g_static_pool))
  Dim nums() As Integer = ArenaAlloc(ByRef embedded, Integer, 4)
  nums(0) = 10
  nums(3) = 40
  printf("static_arena used=%ld rem=%ld nums=%d,%d\n", ArenaUsed(ByRef embedded), ArenaRemaining(ByRef embedded), nums(0), nums(3))
  Return 0
End Function
