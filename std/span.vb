' VerBose Standard Library: Explicit Bounds-Checked Memory Spans (std/span.vb)
' Zero-GC, zero-libc, bare-metal-safe length-carried views over HeapAlloc, ArenaAlloc, Static, or Dim buffers.
' Provides explicit checked access without adding hidden fat-pointer overhead to raw arrays or pointers.

Public Structure ByteSpan
  Dim data As Pointer
  Dim length As Long
End Structure

Public Structure IntSpan
  Dim data As Pointer
  Dim length As Long
End Structure

Public Structure LongSpan
  Dim data As Pointer
  Dim length As Long
End Structure

Public Structure SingleSpan
  Dim data As Pointer
  Dim length As Long
End Structure

Public Structure PtrSpan
  Dim data As Pointer
  Dim length As Long
End Structure

' =============================================================================
' ByteSpan
' =============================================================================

Public Function ByteSpanFrom(buf As Pointer, length As Long) As ByteSpan
  If IsNull(buf) Or (length <= 0) Then
    Return ByteSpan With ( data = 0, length = 0 )
  End If
  Return ByteSpan With ( data = buf, length = length )
End Function

Public Function ByteSpanInBounds(s As ByteSpan, idx As Long) As Boolean
  Return (Not IsNull(s.data)) And (idx >= 0) And (idx < s.length)
End Function

Public Function ByteSpanGet(s As ByteSpan, idx As Long, ByRef outVal As Byte) As Boolean
  If Not ByteSpanInBounds(s, idx) Then
    Return False
  End If
  outVal = PtrRead(PtrAdd(s.data, idx), Byte)
  Return True
End Function

Public Function ByteSpanSet(s As ByteSpan, idx As Long, val As Byte) As Boolean
  If Not ByteSpanInBounds(s, idx) Then
    Return False
  End If
  PtrWrite(PtrAdd(s.data, idx), Byte, val)
  Return True
End Function

Public Function ByteSpanAt(s As ByteSpan, idx As Long) As Byte
  Assert ByteSpanInBounds(s, idx), "ByteSpan index out of bounds"
  Return PtrRead(PtrAdd(s.data, idx), Byte)
End Function

Public Function ByteSpanSub(s As ByteSpan, start, count As Long) As ByteSpan
  If IsNull(s.data) Or (start < 0) Or (count <= 0) Or (start >= s.length) Then
    Return ByteSpan With ( data = 0, length = 0 )
  End If
  Dim avail As Long = s.length - start
  Dim take As Long = count
  If take > avail Then
    take = avail
  End If
  Return ByteSpan With ( data = PtrAdd(s.data, start), length = take )
End Function

' =============================================================================
' IntSpan
' =============================================================================

Public Function IntSpanFrom(buf As Pointer, length As Long) As IntSpan
  If IsNull(buf) Or (length <= 0) Then
    Return IntSpan With ( data = 0, length = 0 )
  End If
  Return IntSpan With ( data = buf, length = length )
End Function

Public Function IntSpanInBounds(s As IntSpan, idx As Long) As Boolean
  Return (Not IsNull(s.data)) And (idx >= 0) And (idx < s.length)
End Function

Public Function IntSpanGet(s As IntSpan, idx As Long, ByRef outVal As Integer) As Boolean
  If Not IntSpanInBounds(s, idx) Then
    Return False
  End If
  outVal = PtrRead(PtrAdd(s.data, idx * Long(SizeOf(Integer))), Integer)
  Return True
End Function

Public Function IntSpanSet(s As IntSpan, idx As Long, val As Integer) As Boolean
  If Not IntSpanInBounds(s, idx) Then
    Return False
  End If
  PtrWrite(PtrAdd(s.data, idx * Long(SizeOf(Integer))), Integer, val)
  Return True
End Function

Public Function IntSpanAt(s As IntSpan, idx As Long) As Integer
  Assert IntSpanInBounds(s, idx), "IntSpan index out of bounds"
  Return PtrRead(PtrAdd(s.data, idx * Long(SizeOf(Integer))), Integer)
End Function

Public Function IntSpanSub(s As IntSpan, start, count As Long) As IntSpan
  If IsNull(s.data) Or (start < 0) Or (count <= 0) Or (start >= s.length) Then
    Return IntSpan With ( data = 0, length = 0 )
  End If
  Dim avail As Long = s.length - start
  Dim take As Long = count
  If take > avail Then
    take = avail
  End If
  Return IntSpan With ( data = PtrAdd(s.data, start * Long(SizeOf(Integer))), length = take )
End Function

' =============================================================================
' LongSpan
' =============================================================================

Public Function LongSpanFrom(buf As Pointer, length As Long) As LongSpan
  If IsNull(buf) Or (length <= 0) Then
    Return LongSpan With ( data = 0, length = 0 )
  End If
  Return LongSpan With ( data = buf, length = length )
End Function

Public Function LongSpanInBounds(s As LongSpan, idx As Long) As Boolean
  Return (Not IsNull(s.data)) And (idx >= 0) And (idx < s.length)
End Function

Public Function LongSpanGet(s As LongSpan, idx As Long, ByRef outVal As Long) As Boolean
  If Not LongSpanInBounds(s, idx) Then
    Return False
  End If
  outVal = PtrRead(PtrAdd(s.data, idx * Long(SizeOf(Long))), Long)
  Return True
End Function

Public Function LongSpanSet(s As LongSpan, idx As Long, val As Long) As Boolean
  If Not LongSpanInBounds(s, idx) Then
    Return False
  End If
  PtrWrite(PtrAdd(s.data, idx * Long(SizeOf(Long))), Long, val)
  Return True
End Function

Public Function LongSpanAt(s As LongSpan, idx As Long) As Long
  Assert LongSpanInBounds(s, idx), "LongSpan index out of bounds"
  Return PtrRead(PtrAdd(s.data, idx * Long(SizeOf(Long))), Long)
End Function

Public Function LongSpanSub(s As LongSpan, start, count As Long) As LongSpan
  If IsNull(s.data) Or (start < 0) Or (count <= 0) Or (start >= s.length) Then
    Return LongSpan With ( data = 0, length = 0 )
  End If
  Dim avail As Long = s.length - start
  Dim take As Long = count
  If take > avail Then
    take = avail
  End If
  Return LongSpan With ( data = PtrAdd(s.data, start * Long(SizeOf(Long))), length = take )
End Function

' =============================================================================
' SingleSpan
' =============================================================================

Public Function SingleSpanFrom(buf As Pointer, length As Long) As SingleSpan
  If IsNull(buf) Or (length <= 0) Then
    Return SingleSpan With ( data = 0, length = 0 )
  End If
  Return SingleSpan With ( data = buf, length = length )
End Function

Public Function SingleSpanInBounds(s As SingleSpan, idx As Long) As Boolean
  Return (Not IsNull(s.data)) And (idx >= 0) And (idx < s.length)
End Function

Public Function SingleSpanGet(s As SingleSpan, idx As Long, ByRef outVal As Single) As Boolean
  If Not SingleSpanInBounds(s, idx) Then
    Return False
  End If
  outVal = PtrRead(PtrAdd(s.data, idx * Long(SizeOf(Single))), Single)
  Return True
End Function

Public Function SingleSpanSet(s As SingleSpan, idx As Long, val As Single) As Boolean
  If Not SingleSpanInBounds(s, idx) Then
    Return False
  End If
  PtrWrite(PtrAdd(s.data, idx * Long(SizeOf(Single))), Single, val)
  Return True
End Function

Public Function SingleSpanAt(s As SingleSpan, idx As Long) As Single
  Assert SingleSpanInBounds(s, idx), "SingleSpan index out of bounds"
  Return PtrRead(PtrAdd(s.data, idx * Long(SizeOf(Single))), Single)
End Function

Public Function SingleSpanSub(s As SingleSpan, start, count As Long) As SingleSpan
  If IsNull(s.data) Or (start < 0) Or (count <= 0) Or (start >= s.length) Then
    Return SingleSpan With ( data = 0, length = 0 )
  End If
  Dim avail As Long = s.length - start
  Dim take As Long = count
  If take > avail Then
    take = avail
  End If
  Return SingleSpan With ( data = PtrAdd(s.data, start * Long(SizeOf(Single))), length = take )
End Function

' =============================================================================
' PtrSpan
' =============================================================================

Public Function PtrSpanFrom(buf As Pointer, length As Long) As PtrSpan
  If IsNull(buf) Or (length <= 0) Then
    Return PtrSpan With ( data = 0, length = 0 )
  End If
  Return PtrSpan With ( data = buf, length = length )
End Function

Public Function PtrSpanInBounds(s As PtrSpan, idx As Long) As Boolean
  Return (Not IsNull(s.data)) And (idx >= 0) And (idx < s.length)
End Function

Public Function PtrSpanGet(s As PtrSpan, idx As Long, ByRef outVal As Pointer) As Boolean
  If Not PtrSpanInBounds(s, idx) Then
    Return False
  End If
  outVal = PtrRead(PtrAdd(s.data, idx * Long(SizeOf(Pointer))), Pointer)
  Return True
End Function

Public Function PtrSpanSet(s As PtrSpan, idx As Long, val As Pointer) As Boolean
  If Not PtrSpanInBounds(s, idx) Then
    Return False
  End If
  PtrWrite(PtrAdd(s.data, idx * Long(SizeOf(Pointer))), Pointer, val)
  Return True
End Function

Public Function PtrSpanAt(s As PtrSpan, idx As Long) As Pointer
  Assert PtrSpanInBounds(s, idx), "PtrSpan index out of bounds"
  Return PtrRead(PtrAdd(s.data, idx * Long(SizeOf(Pointer))), Pointer)
End Function

Public Function PtrSpanSub(s As PtrSpan, start, count As Long) As PtrSpan
  If IsNull(s.data) Or (start < 0) Or (count <= 0) Or (start >= s.length) Then
    Return PtrSpan With ( data = 0, length = 0 )
  End If
  Dim avail As Long = s.length - start
  Dim take As Long = count
  If take > avail Then
    take = avail
  End If
  Return PtrSpan With ( data = PtrAdd(s.data, start * Long(SizeOf(Pointer))), length = take )
End Function
