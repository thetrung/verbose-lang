' Standard Library Collections (std/collections.vb) Test Suite

Include "../std/collections.vb"

Test "IntVec push, pop, get, set, clear, contains, and capacity bounds"
  Dim a As Arena = ArenaCreate(1024)
  Dim v As IntVec = IntVecNew(ByRef a, 4)
  Assert v.length = 0
  Assert v.cap = 4

  Dim out As Integer = -999
  Assert Not IntVecPop(ByRef v, ByRef out), "pop from empty IntVec must fail"
  Assert out = -999

  Assert IntVecPush(ByRef v, 10)
  Assert IntVecPush(ByRef v, 20)
  Assert IntVecPush(ByRef v, 30)
  Assert IntVecPush(ByRef v, 40)
  Assert Not IntVecPush(ByRef v, 50), "push to full IntVec must fail"
  Assert v.length = 4

  Assert IntVecGet(ByRef v, 0) = 10
  Assert IntVecGet(ByRef v, 3) = 40
  Assert IntVecGet(ByRef v, -1) = 0
  Assert IntVecGet(ByRef v, 4) = 0

  Assert IntVecSet(ByRef v, 1, 99)
  Assert Not IntVecSet(ByRef v, -1, 1)
  Assert Not IntVecSet(ByRef v, 4, 1)
  Assert IntVecGet(ByRef v, 1) = 99

  Assert IntVecContains(ByRef v, 99)
  Assert Not IntVecContains(ByRef v, 20)
  Assert Not IntVecContains(ByRef v, 777)

  Assert IntVecPop(ByRef v, ByRef out)
  Assert out = 40
  Assert v.length = 3

  IntVecClear(ByRef v)
  Assert v.length = 0
  Assert Not IntVecContains(ByRef v, 10)
  Assert IntVecPush(ByRef v, 55)
  Assert IntVecGet(ByRef v, 0) = 55

  Dim bad As IntVec = IntVecNew(ByRef a, 0)
  Assert bad.cap = 0
  Assert Not IntVecPush(ByRef bad, 1)

  ArenaDestroy(ByRef a)
End Test

Test "IntVecSort sorts empty, single, reverse, negative, and duplicate values"
  Dim a As Arena = ArenaCreate(1024)

  Dim v0 As IntVec = IntVecNew(ByRef a, 4)
  IntVecSort(ByRef v0)
  Assert v0.length = 0

  Dim v1 As IntVec = IntVecNew(ByRef a, 4)
  Assert IntVecPush(ByRef v1, 42)
  IntVecSort(ByRef v1)
  Assert IntVecGet(ByRef v1, 0) = 42

  Dim v As IntVec = IntVecNew(ByRef a, 10)
  Assert IntVecPush(ByRef v, 5)
  Assert IntVecPush(ByRef v, -3)
  Assert IntVecPush(ByRef v, 99)
  Assert IntVecPush(ByRef v, 0)
  Assert IntVecPush(ByRef v, -3)
  Assert IntVecPush(ByRef v, 12)
  Assert IntVecPush(ByRef v, -100)
  Assert IntVecPush(ByRef v, 7)

  IntVecSort(ByRef v)
  Assert v.length = 8
  Assert IntVecGet(ByRef v, 0) = -100
  Assert IntVecGet(ByRef v, 1) = -3
  Assert IntVecGet(ByRef v, 2) = -3
  Assert IntVecGet(ByRef v, 3) = 0
  Assert IntVecGet(ByRef v, 4) = 5
  Assert IntVecGet(ByRef v, 5) = 7
  Assert IntVecGet(ByRef v, 6) = 12
  Assert IntVecGet(ByRef v, 7) = 99

  ArenaDestroy(ByRef a)
End Test

Test "PtrVec push, pop, get, set, clear, and bounds"
  Dim a As Arena = ArenaCreate(1024)
  Dim pv As PtrVec = PtrVecNew(ByRef a, 3)
  Assert pv.length = 0
  Assert pv.cap = 3

  Dim p1 As Pointer = ArenaAllocBytes(ByRef a, 8)
  Dim p2 As Pointer = ArenaAllocBytes(ByRef a, 8)
  Dim p3 As Pointer = ArenaAllocBytes(ByRef a, 8)
  p1(0) = 65
  p2(0) = 66
  p3(0) = 67

  Dim outP As Pointer
  Assert Not PtrVecPop(ByRef pv, ByRef outP), "pop from empty PtrVec must fail"
  Assert IsNull(PtrVecGet(ByRef pv, 0))

  Assert PtrVecPush(ByRef pv, p1)
  Assert PtrVecPush(ByRef pv, p2)
  Assert PtrVecPush(ByRef pv, p3)
  Assert Not PtrVecPush(ByRef pv, p1), "push to full PtrVec must fail"
  Assert pv.length = 3

  Dim g0 As Pointer = PtrVecGet(ByRef pv, 0)
  Dim g2 As Pointer = PtrVecGet(ByRef pv, 2)
  Assert Not IsNull(g0)
  Assert g0(0) = 65
  Assert g2(0) = 67
  Assert IsNull(PtrVecGet(ByRef pv, -1))
  Assert IsNull(PtrVecGet(ByRef pv, 3))

  Assert PtrVecSet(ByRef pv, 1, p3)
  Assert Not PtrVecSet(ByRef pv, 3, p1)
  Dim g1 As Pointer = PtrVecGet(ByRef pv, 1)
  Assert g1(0) = 67

  Assert PtrVecPop(ByRef pv, ByRef outP)
  Assert outP(0) = 67
  Assert pv.length = 2

  PtrVecClear(ByRef pv)
  Assert pv.length = 0
  Assert IsNull(PtrVecGet(ByRef pv, 0))

  ArenaDestroy(ByRef a)
End Test

Test "IntRing FIFO ordering, full/empty checks, and wraparound"
  Dim a As Arena = ArenaCreate(1024)
  Dim r As IntRing = IntRingNew(ByRef a, 4)

  Assert IntRingIsEmpty(ByRef r)
  Assert Not IntRingIsFull(ByRef r)
  Assert IntRingLen(ByRef r) = 0

  Dim out As Integer = -1
  Assert Not IntRingPop(ByRef r, ByRef out)

  Assert IntRingPush(ByRef r, 10)
  Assert IntRingPush(ByRef r, 20)
  Assert IntRingPush(ByRef r, 30)
  Assert IntRingPush(ByRef r, 40)
  Assert IntRingIsFull(ByRef r)
  Assert IntRingLen(ByRef r) = 4
  Assert Not IntRingPush(ByRef r, 50), "push to full IntRing must fail"

  Assert IntRingPop(ByRef r, ByRef out)
  Assert out = 10
  Assert IntRingPop(ByRef r, ByRef out)
  Assert out = 20
  Assert IntRingLen(ByRef r) = 2
  Assert Not IntRingIsFull(ByRef r)

  ' Wraparound pushes
  Assert IntRingPush(ByRef r, 50)
  Assert IntRingPush(ByRef r, 60)
  Assert IntRingIsFull(ByRef r)
  Assert IntRingLen(ByRef r) = 4
  Assert Not IntRingPush(ByRef r, 70)

  Assert IntRingPop(ByRef r, ByRef out)
  Assert out = 30
  Assert IntRingPop(ByRef r, ByRef out)
  Assert out = 40
  Assert IntRingPop(ByRef r, ByRef out)
  Assert out = 50
  Assert IntRingPop(ByRef r, ByRef out)
  Assert out = 60
  Assert IntRingIsEmpty(ByRef r)
  Assert IntRingLen(ByRef r) = 0
  Assert Not IntRingPop(ByRef r, ByRef out)

  ArenaDestroy(ByRef a)
End Test

Test "IntMap put, get, has, update, collisions, tombstone removal, and re-insertion"
  Dim a As Arena = ArenaCreate(4096)
  ' Small capacity (4) forces hash collisions and full-table probing
  Dim m As IntMap = IntMapNew(ByRef a, 4)
  Assert IntMapLen(ByRef m) = 0
  Assert Not IntMapHas(ByRef m, 1)

  Dim val As Integer = -1
  Assert Not IntMapGet(ByRef m, 1, ByRef val)

  ' Insert keys including negative and zero
  Assert IntMapPut(ByRef m, 0, 100)
  Assert IntMapPut(ByRef m, 4, 200)
  Assert IntMapPut(ByRef m, -7, 300)
  Assert IntMapPut(ByRef m, 15, 400)
  Assert IntMapLen(ByRef m) = 4

  ' Full map rejects new key, but allows updating existing key
  Assert Not IntMapPut(ByRef m, 99, 500), "full IntMap must reject new key"
  Assert IntMapPut(ByRef m, 4, 222), "updating existing key in full IntMap must succeed"
  Assert IntMapLen(ByRef m) = 4
  Assert IntMapGet(ByRef m, 4, ByRef val)
  Assert val = 222

  ' Remove middle element in probe chain -> tombstone must not break lookup of later keys
  Assert IntMapRemove(ByRef m, 4)
  Assert Not IntMapRemove(ByRef m, 4), "removing already-removed key must return False"
  Assert IntMapLen(ByRef m) = 3
  Assert Not IntMapHas(ByRef m, 4)
  Assert IntMapHas(ByRef m, 0)
  Assert IntMapHas(ByRef m, -7)
  Assert IntMapHas(ByRef m, 15)

  Assert IntMapGet(ByRef m, -7, ByRef val)
  Assert val = 300
  Assert IntMapGet(ByRef m, 15, ByRef val)
  Assert val = 400

  ' Re-insert into tombstone slot
  Assert IntMapPut(ByRef m, 99, 500)
  Assert IntMapLen(ByRef m) = 4
  Assert IntMapGet(ByRef m, 99, ByRef val)
  Assert val = 500

  ' Update existing key when table has tombstones (remove 0, update 15)
  Assert IntMapRemove(ByRef m, 0)
  Assert IntMapLen(ByRef m) = 3
  Assert IntMapPut(ByRef m, 15, 444)
  Assert IntMapLen(ByRef m) = 3
  Assert IntMapGet(ByRef m, 15, ByRef val)
  Assert val = 444

  ArenaDestroy(ByRef a)
End Test

Test "OOM Arena handling and larger IntMap stress test"
  Dim tiny As Arena = ArenaCreate(8)
  Dim oomVec As IntVec = IntVecNew(ByRef tiny, 100)
  Assert oomVec.cap = 0
  Assert Not IntVecPush(ByRef oomVec, 1)

  Dim oomMap As IntMap = IntMapNew(ByRef tiny, 100)
  Assert oomMap.cap = 0
  Assert Not IntMapPut(ByRef oomMap, 1, 2)
  ArenaDestroy(ByRef tiny)

  Dim a As Arena = ArenaCreate(4096)
  Dim m As IntMap = IntMapNew(ByRef a, 32)
  Dim i As Integer = 0
  While i < 20 Do
    Assert IntMapPut(ByRef m, i * 3 - 10, i * 100)
    i = i + 1
  End While
  Assert IntMapLen(ByRef m) = 20

  i = 0
  While i < 20 Do
    If (i Mod 2) = 0 Then
      Assert IntMapRemove(ByRef m, i * 3 - 10)
    End If
    i = i + 1
  End While
  Assert IntMapLen(ByRef m) = 10

  i = 0
  Dim v As Integer = 0
  While i < 20 Do
    If (i Mod 2) = 0 Then
      Assert Not IntMapHas(ByRef m, i * 3 - 10)
    Else
      Assert IntMapGet(ByRef m, i * 3 - 10, ByRef v)
      Assert v = i * 100
    End If
    i = i + 1
  End While

  ArenaDestroy(ByRef a)
End Test
