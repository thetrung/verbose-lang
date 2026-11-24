' std/strings.vb — Arena-backed String, StrView, and StrBuilder utilities.
' Pure VerBose v0.4.0 implementation (no libc string dependencies; bare-metal safe).

Public Structure StrView
  Dim data As Pointer
  Dim length As Integer
End Structure

Public Structure StrBuilder
  Dim buf As Pointer
  Dim length, cap As Integer
End Structure

Private Function ByteAt(s As Pointer, i As Integer) As Integer
  Dim b As Integer = s(i)
  Return b And 255
End Function

Private Function IsSpaceByte(c As Integer) As Boolean
  If c = 32 Then
    Return True
  End If
  If c = 9 Then
    Return True
  End If
  If c = 10 Then
    Return True
  End If
  If c = 13 Then
    Return True
  End If
  Return False
End Function

Public Function StrLen(s As Pointer) As Integer
  If IsNull(s) Then
    Return 0
  End If
  Dim i As Integer = 0
  While ByteAt(s, i) <> 0 Do
    i = i + 1
  End While
  Return i
End Function

Public Function StrCmp(a As Pointer, b As Pointer) As Integer
  If IsNull(a) Then
    If IsNull(b) Then
      Return 0
    End If
    Return 0 - ByteAt(b, 0)
  End If
  If IsNull(b) Then
    Return ByteAt(a, 0)
  End If
  Dim i As Integer = 0
  Dim done As Boolean = False
  Dim res As Integer = 0
  While Not done Do
    Dim ca As Integer = ByteAt(a, i)
    Dim cb As Integer = ByteAt(b, i)
    If ca <> cb Then
      res = ca - cb
      done = True
    Else
      If ca = 0 Then
        res = 0
        done = True
      Else
        i = i + 1
      End If
    End If
  End While
  Return res
End Function

Public Function StrEq(a As Pointer, b As Pointer) As Boolean
  Return StrCmp(a, b) = 0
End Function

Public Function StrStartsWith(s As Pointer, prefix As Pointer) As Boolean
  If IsNull(prefix) Then
    Return True
  End If
  If IsNull(s) Then
    Return ByteAt(prefix, 0) = 0
  End If
  Dim i As Integer = 0
  Dim cp As Integer = ByteAt(prefix, 0)
  While cp <> 0 Do
    Dim cs As Integer = ByteAt(s, i)
    If cs <> cp Then
      Return False
    End If
    i = i + 1
    cp = ByteAt(prefix, i)
  End While
  Return True
End Function

Public Function StrEndsWith(s As Pointer, suffix As Pointer) As Boolean
  If IsNull(suffix) Then
    Return True
  End If
  Dim sufLen As Integer = StrLen(suffix)
  If sufLen = 0 Then
    Return True
  End If
  Dim sLen As Integer = StrLen(s)
  If sufLen > sLen Then
    Return False
  End If
  Dim off As Integer = sLen - sufLen
  For i = 0 To sufLen - 1
    Dim cs As Integer = ByteAt(s, off + i)
    Dim cp As Integer = ByteAt(suffix, i)
    If cs <> cp Then
      Return False
    End If
  End For
  Return True
End Function

Public Function StrIndexOf(s As Pointer, ch As Integer) As Integer
  If IsNull(s) Then
    Return -1
  End If
  Dim target As Integer = ch And 255
  Dim i As Integer = 0
  Dim c As Integer = ByteAt(s, 0)
  While c <> 0 Do
    If c = target Then
      Return i
    End If
    i = i + 1
    c = ByteAt(s, i)
  End While
  Return -1
End Function

Public Function StrFind(haystack As Pointer, needle As Pointer) As Integer
  If IsNull(needle) Then
    Return 0
  End If
  Dim nLen As Integer = StrLen(needle)
  If nLen = 0 Then
    Return 0
  End If
  If IsNull(haystack) Then
    Return -1
  End If
  Dim hLen As Integer = StrLen(haystack)
  If nLen > hLen Then
    Return -1
  End If
  For i = 0 To hLen - nLen
    Dim j As Integer = 0
    Dim matched As Boolean = True
    While j < nLen Do
      Dim ch As Integer = ByteAt(haystack, i + j)
      Dim cn As Integer = ByteAt(needle, j)
      If ch <> cn Then
        matched = False
        j = nLen
      Else
        j = j + 1
      End If
    End While
    If matched Then
      Return i
    End If
  End For
  Return -1
End Function

Public Function StrCopy(ByRef a As Arena, s As Pointer) As Pointer
  Dim n As Integer = StrLen(s)
  Dim dst As Pointer = ArenaAllocBytes(ByRef a, n + 1)
  If IsNull(dst) Then
    Return 0
  End If
  If Not IsNull(s) Then
    For i = 0 To n - 1
      dst(i) = s(i)
    End For
  End If
  dst(n) = 0
  Return dst
End Function

Public Function StrSubstr(ByRef a As Arena, s As Pointer, start As Integer, length As Integer) As Pointer
  Dim st As Integer = start
  If st < 0 Then
    st = 0
  End If
  Dim sLen As Integer = StrLen(s)
  If st >= sLen Then
    Return StrCopy(ByRef a, "")
  End If
  If length <= 0 Then
    Return StrCopy(ByRef a, "")
  End If
  Dim avail As Integer = sLen - st
  Dim take As Integer = length
  If take > avail Then
    take = avail
  End If
  Dim dst As Pointer = ArenaAllocBytes(ByRef a, take + 1)
  If IsNull(dst) Then
    Return 0
  End If
  For i = 0 To take - 1
    dst(i) = s(st + i)
  End For
  dst(take) = 0
  Return dst
End Function

Public Function StrConcat(ByRef a As Arena, aStr As Pointer, bStr As Pointer) As Pointer
  Dim lenA As Integer = StrLen(aStr)
  Dim lenB As Integer = StrLen(bStr)
  Dim dst As Pointer = ArenaAllocBytes(ByRef a, lenA + lenB + 1)
  If IsNull(dst) Then
    Return 0
  End If
  If Not IsNull(aStr) Then
    For i = 0 To lenA - 1
      dst(i) = aStr(i)
    End For
  End If
  If Not IsNull(bStr) Then
    For i = 0 To lenB - 1
      dst(lenA + i) = bStr(i)
    End For
  End If
  dst(lenA + lenB) = 0
  Return dst
End Function

Public Function StrTrim(ByRef a As Arena, s As Pointer) As Pointer
  If IsNull(s) Then
    Return StrCopy(ByRef a, "")
  End If
  Dim n As Integer = StrLen(s)
  Dim lo As Integer = 0
  Dim scanning As Boolean = True
  While scanning Do
    If lo >= n Then
      scanning = False
    Else
      If IsSpaceByte(ByteAt(s, lo)) Then
        lo = lo + 1
      Else
        scanning = False
      End If
    End If
  End While
  Dim hi As Integer = n
  scanning = True
  While scanning Do
    If hi <= lo Then
      scanning = False
    Else
      If IsSpaceByte(ByteAt(s, hi - 1)) Then
        hi = hi - 1
      Else
        scanning = False
      End If
    End If
  End While
  Return StrSubstr(ByRef a, s, lo, hi - lo)
End Function

Public Function StrToUpper(ByRef a As Arena, s As Pointer) As Pointer
  Dim n As Integer = StrLen(s)
  Dim dst As Pointer = ArenaAllocBytes(ByRef a, n + 1)
  If IsNull(dst) Then
    Return 0
  End If
  If Not IsNull(s) Then
    For i = 0 To n - 1
      Dim c As Integer = ByteAt(s, i)
      If c >= 97 Then
        If c <= 122 Then
          c = c - 32
        End If
      End If
      dst(i) = c
    End For
  End If
  dst(n) = 0
  Return dst
End Function

Public Function StrToLower(ByRef a As Arena, s As Pointer) As Pointer
  Dim n As Integer = StrLen(s)
  Dim dst As Pointer = ArenaAllocBytes(ByRef a, n + 1)
  If IsNull(dst) Then
    Return 0
  End If
  If Not IsNull(s) Then
    For i = 0 To n - 1
      Dim c As Integer = ByteAt(s, i)
      If c >= 65 Then
        If c <= 90 Then
          c = c + 32
        End If
      End If
      dst(i) = c
    End For
  End If
  dst(n) = 0
  Return dst
End Function

Public Function StrToInt(s As Pointer) As Integer
  If IsNull(s) Then
    Return 0
  End If
  Dim i As Integer = 0
  Dim skipping As Boolean = True
  While skipping Do
    Dim c As Integer = ByteAt(s, i)
    If IsSpaceByte(c) Then
      i = i + 1
    Else
      skipping = False
    End If
  End While
  Dim neg As Boolean = False
  Dim first As Integer = ByteAt(s, i)
  If first = 45 Then
    neg = True
    i = i + 1
  Else
    If first = 43 Then
      i = i + 1
    End If
  End If
  Dim acc As Integer = 0
  Dim parsing As Boolean = True
  While parsing Do
    Dim c As Integer = ByteAt(s, i)
    If c >= 48 Then
      If c <= 57 Then
        acc = acc * 10 - (c - 48)
        i = i + 1
      Else
        parsing = False
      End If
    Else
      parsing = False
    End If
  End While
  If neg Then
    Return acc
  End If
  Return 0 - acc
End Function

Public Function IntToStr(ByRef a As Arena, n As Integer) As Pointer
  If n = 0 Then
    Return StrCopy(ByRef a, "0")
  End If
  Dim v As Integer = n
  Dim neg As Boolean = False
  If v < 0 Then
    neg = True
  Else
    v = 0 - v
  End If
  Dim rev(32) As Byte
  Dim count As Integer = 0
  While v < 0 Do
    Dim digit As Integer = 0 - (v Mod 10)
    rev(count) = 48 + digit
    count = count + 1
    v = v / 10
  End While
  Dim total As Integer = count
  If neg Then
    total = total + 1
  End If
  Dim dst As Pointer = ArenaAllocBytes(ByRef a, total + 1)
  If IsNull(dst) Then
    Return 0
  End If
  Dim pos As Integer = 0
  If neg Then
    dst(0) = 45
    pos = 1
  End If
  For i = 0 To count - 1
    dst(pos + i) = rev(count - 1 - i)
  End For
  dst(total) = 0
  Return dst
End Function

Public Function StrViewFrom(s As Pointer) As StrView
  Return StrView(s, StrLen(s))
End Function

Public Function StrViewEq(a As StrView, b As StrView) As Boolean
  If a.length <> b.length Then
    Return False
  End If
  If a.length <= 0 Then
    Return True
  End If
  If IsNull(a.data) Then
    Return False
  End If
  If IsNull(b.data) Then
    Return False
  End If
  For i = 0 To a.length - 1
    Dim ca As Integer = ByteAt(a.data, i)
    Dim cb As Integer = ByteAt(b.data, i)
    If ca <> cb Then
      Return False
    End If
  End For
  Return True
End Function

Public Function SbNew(ByRef a As Arena, cap As Integer) As StrBuilder
  Dim c As Integer = cap
  If c < 1 Then
    c = 1
  End If
  Dim buf As Pointer = ArenaAllocBytes(ByRef a, c)
  If IsNull(buf) Then
    Return StrBuilder(0, 0, 0)
  End If
  buf(0) = 0
  Return StrBuilder(buf, 0, c)
End Function

Public Sub SbAppendChar(ByRef sb As StrBuilder, ch As Integer)
  If IsNull(sb.buf) Then
    Return
  End If
  If sb.length + 1 >= sb.cap Then
    Return
  End If
  sb.buf(sb.length) = ch And 255
  sb.length = sb.length + 1
  sb.buf(sb.length) = 0
End Sub

Public Sub SbAppend(ByRef sb As StrBuilder, s As Pointer)
  If IsNull(sb.buf) Then
    Return
  End If
  If IsNull(s) Then
    Return
  End If
  Dim i As Integer = 0
  Dim writing As Boolean = True
  While writing Do
    Dim c As Integer = ByteAt(s, i)
    If c = 0 Then
      writing = False
    Else
      If sb.length + 1 >= sb.cap Then
        writing = False
      Else
        sb.buf(sb.length) = c
        sb.length = sb.length + 1
        i = i + 1
      End If
    End If
  End While
  sb.buf(sb.length) = 0
End Sub

Public Sub SbAppendInt(ByRef sb As StrBuilder, n As Integer)
  If IsNull(sb.buf) Then
    Return
  End If
  If n = 0 Then
    SbAppendChar(ByRef sb, 48)
    Return
  End If
  Dim v As Integer = n
  If v < 0 Then
    SbAppendChar(ByRef sb, 45)
  Else
    v = 0 - v
  End If
  Dim rev(32) As Byte
  Dim count As Integer = 0
  While v < 0 Do
    Dim digit As Integer = 0 - (v Mod 10)
    rev(count) = 48 + digit
    count = count + 1
    v = v / 10
  End While
  For i = 0 To count - 1
    SbAppendChar(ByRef sb, rev(count - 1 - i) And 255)
  End For
End Sub

Public Function SbFinish(ByRef sb As StrBuilder) As Pointer
  If IsNull(sb.buf) Then
    Return 0
  End If
  sb.buf(sb.length) = 0
  Return sb.buf
End Function
