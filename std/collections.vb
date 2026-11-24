' VerBose Standard Library: Arena-backed Deterministic Collections
' Zero-GC, fixed-capacity, Arena-allocated data structures.

Public Structure IntVec
  Dim data As Pointer
  Dim length, cap As Integer
End Structure

Public Structure PtrVec
  Dim data As Pointer
  Dim length, cap As Integer
End Structure

Public Structure IntRing
  Dim data As Pointer
  Dim head, tail, count, cap As Integer
End Structure

Public Structure IntMap
  Dim keys, vals, states As Pointer
  Dim count, cap As Integer
End Structure

' =============================================================================
' IntVec: Arena-backed dynamic-length integer vector with fixed capacity
' =============================================================================

Public Function IntVecNew(ByRef a As Arena, cap As Integer) As IntVec
  Dim empty As IntVec
  empty.length = 0
  empty.cap = 0
  If cap <= 0 Then
    Return empty
  End If
  Dim buf() As Integer = ArenaAlloc(ByRef a, Integer, cap)
  If IsNull(buf) Then
    Return empty
  End If
  Dim v As IntVec With ( data = buf, length = 0, cap = cap )
  Return v
End Function

Public Function IntVecPush(ByRef v As IntVec, val As Integer) As Boolean
  If IsNull(v.data) Then
    Return False
  End If
  If v.length >= v.cap Then
    Return False
  End If
  Dim arr() As Integer = v.data
  arr(v.length) = val
  v.length = v.length + 1
  Return True
End Function

Public Function IntVecPop(ByRef v As IntVec, ByRef outVal As Integer) As Boolean
  If IsNull(v.data) Then
    Return False
  End If
  If v.length <= 0 Then
    Return False
  End If
  v.length = v.length - 1
  Dim arr() As Integer = v.data
  outVal = arr(v.length)
  Return True
End Function

Public Function IntVecGet(ByRef v As IntVec, idx As Integer) As Integer
  If IsNull(v.data) Then
    Return 0
  End If
  If idx < 0 Then
    Return 0
  End If
  If idx >= v.length Then
    Return 0
  End If
  Dim arr() As Integer = v.data
  Return arr(idx)
End Function

Public Function IntVecSet(ByRef v As IntVec, idx, val As Integer) As Boolean
  If IsNull(v.data) Then
    Return False
  End If
  If idx < 0 Then
    Return False
  End If
  If idx >= v.length Then
    Return False
  End If
  Dim arr() As Integer = v.data
  arr(idx) = val
  Return True
End Function

Public Sub IntVecClear(ByRef v As IntVec)
  v.length = 0
End Sub

Public Function IntVecContains(ByRef v As IntVec, val As Integer) As Boolean
  If IsNull(v.data) Then
    Return False
  End If
  If v.length <= 0 Then
    Return False
  End If
  Dim arr() As Integer = v.data
  Dim i As Integer = 0
  While i < v.length Do
    If arr(i) = val Then
      Return True
    End If
    i = i + 1
  End While
  Return False
End Function

Public Sub IntVecSort(ByRef v As IntVec)
  If IsNull(v.data) Then
    Return
  End If
  If v.length <= 1 Then
    Return
  End If
  Dim arr() As Integer = v.data
  Dim gap As Integer = v.length / 2
  While gap > 0 Do
    Dim i As Integer = gap
    While i < v.length Do
      Dim tmp As Integer = arr(i)
      Dim j As Integer = i
      Dim moving As Boolean = True
      While moving Do
        If j < gap Then
          moving = False
        Else
          If arr(j - gap) > tmp Then
            arr(j) = arr(j - gap)
            j = j - gap
          Else
            moving = False
          End If
        End If
      End While
      arr(j) = tmp
      i = i + 1
    End While
    gap = gap / 2
  End While
End Sub

' =============================================================================
' PtrVec: Arena-backed pointer vector with fixed capacity
' =============================================================================

Public Function PtrVecNew(ByRef a As Arena, cap As Integer) As PtrVec
  Dim empty As PtrVec
  empty.length = 0
  empty.cap = 0
  If cap <= 0 Then
    Return empty
  End If
  Dim buf() As Pointer = ArenaAlloc(ByRef a, Pointer, cap)
  If IsNull(buf) Then
    Return empty
  End If
  Dim v As PtrVec With ( data = buf, length = 0, cap = cap )
  Return v
End Function

Public Function PtrVecPush(ByRef v As PtrVec, val As Pointer) As Boolean
  If IsNull(v.data) Then
    Return False
  End If
  If v.length >= v.cap Then
    Return False
  End If
  Dim arr() As Pointer = v.data
  arr(v.length) = val
  v.length = v.length + 1
  Return True
End Function

Public Function PtrVecPop(ByRef v As PtrVec, ByRef outVal As Pointer) As Boolean
  If IsNull(v.data) Then
    Return False
  End If
  If v.length <= 0 Then
    Return False
  End If
  v.length = v.length - 1
  Dim arr() As Pointer = v.data
  outVal = arr(v.length)
  Return True
End Function

Public Function PtrVecGet(ByRef v As PtrVec, idx As Integer) As Pointer
  Dim nilPtr As Pointer
  If IsNull(v.data) Then
    Return nilPtr
  End If
  If idx < 0 Then
    Return nilPtr
  End If
  If idx >= v.length Then
    Return nilPtr
  End If
  Dim arr() As Pointer = v.data
  Return arr(idx)
End Function

Public Function PtrVecSet(ByRef v As PtrVec, idx As Integer, val As Pointer) As Boolean
  If IsNull(v.data) Then
    Return False
  End If
  If idx < 0 Then
    Return False
  End If
  If idx >= v.length Then
    Return False
  End If
  Dim arr() As Pointer = v.data
  arr(idx) = val
  Return True
End Function

Public Sub PtrVecClear(ByRef v As PtrVec)
  v.length = 0
End Sub

' =============================================================================
' IntRing: Arena-backed circular FIFO integer ring buffer
' =============================================================================

Public Function IntRingNew(ByRef a As Arena, cap As Integer) As IntRing
  Dim empty As IntRing
  empty.head = 0
  empty.tail = 0
  empty.count = 0
  empty.cap = 0
  If cap <= 0 Then
    Return empty
  End If
  Dim buf() As Integer = ArenaAlloc(ByRef a, Integer, cap)
  If IsNull(buf) Then
    Return empty
  End If
  Dim r As IntRing With ( data = buf, head = 0, tail = 0, count = 0, cap = cap )
  Return r
End Function

Public Function IntRingPush(ByRef r As IntRing, val As Integer) As Boolean
  If IsNull(r.data) Then
    Return False
  End If
  If r.count >= r.cap Then
    Return False
  End If
  Dim arr() As Integer = r.data
  arr(r.tail) = val
  r.tail = (r.tail + 1) Mod r.cap
  r.count = r.count + 1
  Return True
End Function

Public Function IntRingPop(ByRef r As IntRing, ByRef outVal As Integer) As Boolean
  If IsNull(r.data) Then
    Return False
  End If
  If r.count <= 0 Then
    Return False
  End If
  Dim arr() As Integer = r.data
  outVal = arr(r.head)
  r.head = (r.head + 1) Mod r.cap
  r.count = r.count - 1
  Return True
End Function

Public Function IntRingLen(ByRef r As IntRing) As Integer
  Return r.count
End Function

Public Function IntRingIsEmpty(ByRef r As IntRing) As Boolean
  Return r.count = 0
End Function

Public Function IntRingIsFull(ByRef r As IntRing) As Boolean
  Return r.count >= r.cap
End Function

' =============================================================================
' IntMap: Arena-backed open-addressing integer hash map (linear probing)
' States: 0 = empty, 1 = occupied, 2 = tombstone
' =============================================================================

Private Function IntMapHash(key As Integer, cap As Integer) As Integer
  Dim h As Integer = key Xor (key Shr 16)
  h = h * 73244475
  h = h Xor (h Shr 16)
  h = h And 2147483647
  Return h Mod cap
End Function

Private Function IntMapValid(ByRef m As IntMap) As Boolean
  If m.cap <= 0 Then
    Return False
  End If
  If IsNull(m.keys) Then
    Return False
  End If
  If IsNull(m.vals) Then
    Return False
  End If
  If IsNull(m.states) Then
    Return False
  End If
  Return True
End Function

Public Function IntMapNew(ByRef a As Arena, cap As Integer) As IntMap
  Dim empty As IntMap
  empty.count = 0
  empty.cap = 0
  If cap <= 0 Then
    Return empty
  End If
  Dim kBuf() As Integer = ArenaAlloc(ByRef a, Integer, cap)
  If IsNull(kBuf) Then
    Return empty
  End If
  Dim vBuf() As Integer = ArenaAlloc(ByRef a, Integer, cap)
  If IsNull(vBuf) Then
    Return empty
  End If
  Dim sBuf As Pointer = ArenaAllocBytes(ByRef a, cap)
  If IsNull(sBuf) Then
    Return empty
  End If
  Dim m As IntMap With ( keys = kBuf, vals = vBuf, states = sBuf, count = 0, cap = cap )
  Return m
End Function

Public Function IntMapPut(ByRef m As IntMap, key, val As Integer) As Boolean
  If Not IntMapValid(ByRef m) Then
    Return False
  End If
  Dim keysArr() As Integer = m.keys
  Dim valsArr() As Integer = m.vals
  Dim statesArr() As Byte = m.states

  Dim startIdx As Integer = IntMapHash(key, m.cap)
  Dim firstTomb As Integer = -1
  Dim step As Integer = 0

  While step < m.cap Do
    Dim idx As Integer = (startIdx + step) Mod m.cap
    Dim st As Integer = statesArr(idx) And 255
    If st = 1 Then
      If keysArr(idx) = key Then
        valsArr(idx) = val
        Return True
      End If
    Else
      If st = 0 Then
        Dim target As Integer = idx
        If firstTomb >= 0 Then
          target = firstTomb
        End If
        keysArr(target) = key
        valsArr(target) = val
        statesArr(target) = 1
        m.count = m.count + 1
        Return True
      Else
        If firstTomb < 0 Then
          firstTomb = idx
        End If
      End If
    End If
    step = step + 1
  End While

  If firstTomb >= 0 Then
    keysArr(firstTomb) = key
    valsArr(firstTomb) = val
    statesArr(firstTomb) = 1
    m.count = m.count + 1
    Return True
  End If
  Return False
End Function

Public Function IntMapGet(ByRef m As IntMap, key As Integer, ByRef outVal As Integer) As Boolean
  If Not IntMapValid(ByRef m) Then
    Return False
  End If
  Dim keysArr() As Integer = m.keys
  Dim valsArr() As Integer = m.vals
  Dim statesArr() As Byte = m.states

  Dim startIdx As Integer = IntMapHash(key, m.cap)
  Dim step As Integer = 0

  While step < m.cap Do
    Dim idx As Integer = (startIdx + step) Mod m.cap
    Dim st As Integer = statesArr(idx) And 255
    If st = 0 Then
      Return False
    End If
    If st = 1 Then
      If keysArr(idx) = key Then
        outVal = valsArr(idx)
        Return True
      End If
    End If
    step = step + 1
  End While
  Return False
End Function

Public Function IntMapHas(ByRef m As IntMap, key As Integer) As Boolean
  Dim dummy As Integer = 0
  Return IntMapGet(ByRef m, key, ByRef dummy)
End Function

Public Function IntMapRemove(ByRef m As IntMap, key As Integer) As Boolean
  If Not IntMapValid(ByRef m) Then
    Return False
  End If
  Dim keysArr() As Integer = m.keys
  Dim statesArr() As Byte = m.states

  Dim startIdx As Integer = IntMapHash(key, m.cap)
  Dim step As Integer = 0

  While step < m.cap Do
    Dim idx As Integer = (startIdx + step) Mod m.cap
    Dim st As Integer = statesArr(idx) And 255
    If st = 0 Then
      Return False
    End If
    If st = 1 Then
      If keysArr(idx) = key Then
        statesArr(idx) = 2
        m.count = m.count - 1
        Return True
      End If
    End If
    step = step + 1
  End While
  Return False
End Function

Public Function IntMapLen(ByRef m As IntMap) As Integer
  Return m.count
End Function
