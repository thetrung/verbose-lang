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
  For i = 0 To sufLen - 1 Do
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
  For i = 0 To hLen - nLen Do
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
    For i = 0 To n - 1 Do
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
  For i = 0 To take - 1 Do
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
    For i = 0 To lenA - 1 Do
      dst(i) = aStr(i)
    End For
  End If
  If Not IsNull(bStr) Then
    For i = 0 To lenB - 1 Do
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
    For i = 0 To n - 1 Do
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
    For i = 0 To n - 1 Do
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
  For i = 0 To count - 1 Do
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
  For i = 0 To a.length - 1 Do
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
  For i = 0 To count - 1 Do
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

Public Sub SbReset(ByRef sb As StrBuilder)
  sb.length = 0
  If Not IsNull(sb.buf) Then
    sb.buf(0) = 0
  End If
End Sub

Public Sub SbAppendSlice(ByRef sb As StrBuilder, s As Pointer, length As Integer)
  If IsNull(sb.buf) Or IsNull(s) Or (length <= 0) Then
    Return
  End If
  For i = 0 To length - 1 Do
    If (sb.length + 1) >= sb.cap Then
      Break
    End If
    sb.buf(sb.length) = ByteAt(s, i)
    sb.length = sb.length + 1
  End For
  sb.buf(sb.length) = 0
End Sub

Public Sub SbAppendLine(ByRef sb As StrBuilder, s As Pointer)
  SbAppend(ByRef sb, s)
  SbAppendChar(ByRef sb, 10)
End Sub

Public Sub SbAppendBool(ByRef sb As StrBuilder, b As Boolean)
  If b Then
    SbAppend(ByRef sb, "True")
  Else
    SbAppend(ByRef sb, "False")
  End If
End Sub

Public Sub SbAppendLong(ByRef sb As StrBuilder, n As Long)
  If IsNull(sb.buf) Then
    Return
  End If
  If n = 0 Then
    SbAppendChar(ByRef sb, 48)
    Return
  End If
  Dim v As Long = n
  If v < 0 Then
    SbAppendChar(ByRef sb, 45)
  Else
    v = 0 - v
  End If
  Dim rev(32) As Byte
  Dim count As Integer = 0
  While v < 0 Do
    Dim digit As Long = 0 - (v Mod 10)
    rev(count) = 48 + Integer(digit)
    count = count + 1
    v = v / 10
  End While
  For i = 0 To count - 1 Do
    SbAppendChar(ByRef sb, rev(count - 1 - i) And 255)
  End For
End Sub

Public Sub SbAppendHex(ByRef sb As StrBuilder, n As Long)
  If IsNull(sb.buf) Then
    Return
  End If
  If n = 0 Then
    SbAppendChar(ByRef sb, 48)
    Return
  End If
  Dim rev(20) As Byte
  Dim count As Integer = 0
  Dim v As Long = n
  While v <> 0 Do
    Dim nib As Integer = Integer(v And 15)
    If nib < 10 Then
      rev(count) = 48 + nib
    Else
      rev(count) = 87 + nib
    End If
    count = count + 1
    v = v Shr 4
  End While
  For i = 0 To count - 1 Do
    SbAppendChar(ByRef sb, rev(count - 1 - i) And 255)
  End For
End Sub

Public Sub SbAppendDouble(ByRef sb As StrBuilder, v As Double, decimals As Integer)
  If IsNull(sb.buf) Then
    Return
  End If
  If v <> v Then
    SbAppend(ByRef sb, "NaN")
    Return
  End If
  Dim prec As Integer = decimals
  If prec < 0 Then
    prec = 0
  End If
  If prec > 9 Then
    prec = 9
  End If
  Dim val As Double = v
  If val < 0.0 Then
    SbAppendChar(ByRef sb, 45)
    val = 0.0 - val
  End If
  Dim scale As Long = 1
  For pi = 0 To prec - 1 Do
    scale = scale * 10
  End For
  Dim rounded As Long = Long(val * Double(scale) + 0.5)
  Dim intPart As Long = rounded / scale
  Dim fracPart As Long = rounded Mod scale
  SbAppendLong(ByRef sb, intPart)
  If prec > 0 Then
    SbAppendChar(ByRef sb, 46)
    Dim div As Long = scale / 10
    While div > 0 Do
      Dim d As Long = (fracPart / div) Mod 10
      SbAppendChar(ByRef sb, 48 + Integer(d))
      div = div / 10
    End While
  End If
End Sub

Public Function BoolToStr(b As Boolean) As Pointer
  If b Then
    Return "True"
  End If
  Return "False"
End Function

Public Function LongToStr(ByRef a As Arena, n As Long) As Pointer
  Dim sb As StrBuilder = SbNew(ByRef a, 32)
  SbAppendLong(ByRef sb, n)
  Return SbFinish(ByRef sb)
End Function

Public Function HexToStr(ByRef a As Arena, n As Long) As Pointer
  Dim sb As StrBuilder = SbNew(ByRef a, 24)
  SbAppend(ByRef sb, "0x")
  SbAppendHex(ByRef sb, n)
  Return SbFinish(ByRef sb)
End Function

Public Function DoubleToStr(ByRef a As Arena, v As Double, decimals As Integer) As Pointer
  Dim sb As StrBuilder = SbNew(ByRef a, 48)
  SbAppendDouble(ByRef sb, v, decimals)
  Return SbFinish(ByRef sb)
End Function

Public Function StrConcat3(ByRef a As Arena, s1, s2, s3 As Pointer) As Pointer
  Dim total As Integer = StrLen(s1) + StrLen(s2) + StrLen(s3)
  Dim sb As StrBuilder = SbNew(ByRef a, total + 1)
  SbAppend(ByRef sb, s1)
  SbAppend(ByRef sb, s2)
  SbAppend(ByRef sb, s3)
  Return SbFinish(ByRef sb)
End Function

Public Function StrConcat4(ByRef a As Arena, s1, s2, s3, s4 As Pointer) As Pointer
  Dim total As Integer = StrLen(s1) + StrLen(s2) + StrLen(s3) + StrLen(s4)
  Dim sb As StrBuilder = SbNew(ByRef a, total + 1)
  SbAppend(ByRef sb, s1)
  SbAppend(ByRef sb, s2)
  SbAppend(ByRef sb, s3)
  SbAppend(ByRef sb, s4)
  Return SbFinish(ByRef sb)
End Function

Public Function StrJoin(ByRef a As Arena, parts() As Pointer, count As Integer, sep As Pointer) As Pointer
  If count <= 0 Then
    Return StrCopy(ByRef a, "")
  End If
  Dim sepLen As Integer = StrLen(sep)
  Dim total As Integer = 0
  For i = 0 To count - 1 Do
    If i > 0 Then
      total = total + sepLen
    End If
    total = total + StrLen(parts(i))
  End For
  Dim sb As StrBuilder = SbNew(ByRef a, total + 1)
  For i = 0 To count - 1 Do
    If i > 0 Then
      SbAppend(ByRef sb, sep)
    End If
    SbAppend(ByRef sb, parts(i))
  End For
  Return SbFinish(ByRef sb)
End Function

Public Function StrRepeat(ByRef a As Arena, s As Pointer, times As Integer) As Pointer
  If (times <= 0) Or IsNull(s) Then
    Return StrCopy(ByRef a, "")
  End If
  Dim sLen As Integer = StrLen(s)
  Dim sb As StrBuilder = SbNew(ByRef a, sLen * times + 1)
  For i = 0 To times - 1 Do
    SbAppend(ByRef sb, s)
  End For
  Return SbFinish(ByRef sb)
End Function

' Arena-backed zero-libc {} template formatting (escape literal braces with {{ / }})
Public Function StrFmtArgs(ByRef a As Arena, fmt As Pointer, args() As Pointer, argCount As Integer) As Pointer
  If IsNull(fmt) Then
    Return StrCopy(ByRef a, "")
  End If
  Dim fLen As Integer = StrLen(fmt)
  Dim cap As Integer = fLen + 1
  For i = 0 To argCount - 1 Do
    cap = cap + StrLen(args(i))
  End For
  Dim sb As StrBuilder = SbNew(ByRef a, cap)
  Dim i As Integer = 0
  Dim ai As Integer = 0
  While i < fLen Do
    Dim c As Integer = ByteAt(fmt, i)
    If (c = 123) And ((i + 1) < fLen) And (ByteAt(fmt, i + 1) = 123) Then
      SbAppendChar(ByRef sb, 123)
      i = i + 2
    Else
      If (c = 125) And ((i + 1) < fLen) And (ByteAt(fmt, i + 1) = 125) Then
        SbAppendChar(ByRef sb, 125)
        i = i + 2
      Else
        If (c = 123) And ((i + 1) < fLen) And (ByteAt(fmt, i + 1) = 125) Then
          If ai < argCount Then
            SbAppend(ByRef sb, args(ai))
            ai = ai + 1
          End If
          i = i + 2
        Else
          SbAppendChar(ByRef sb, c)
          i = i + 1
        End If
      End If
    End If
  End While
  Return SbFinish(ByRef sb)
End Function

Public Function StrFmt1(ByRef a As Arena, fmt, a1 As Pointer) As Pointer
  Dim args(1) As Pointer = (a1)
  Return StrFmtArgs(ByRef a, fmt, args, 1)
End Function

Public Function StrFmt2(ByRef a As Arena, fmt, a1, a2 As Pointer) As Pointer
  Dim args(2) As Pointer = (a1, a2)
  Return StrFmtArgs(ByRef a, fmt, args, 2)
End Function

Public Function StrFmt3(ByRef a As Arena, fmt, a1, a2, a3 As Pointer) As Pointer
  Dim args(3) As Pointer = (a1, a2, a3)
  Return StrFmtArgs(ByRef a, fmt, args, 3)
End Function
