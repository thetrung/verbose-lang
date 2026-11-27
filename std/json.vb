' std/json.vb — Arena-backed RFC 8259 JSON parser, DOM, and streaming JsonWriter.
' Pure VerBose implementation (zero GC, zero libc dependencies; bare-metal safe).

Include "strings.vb"

Public Enum JsonKind
  JsonInvalid = 0
  JsonNull = 1
  JsonBool = 2
  JsonInt = 3
  JsonFloat = 4
  JsonString = 5
  JsonArray = 6
  JsonObject = 7
End Enum

Public Structure JsonNode
  Dim kind As Integer
  Dim boolVal As Boolean
  Dim intVal As Long
  Dim floatVal As Double
  Dim strVal As Pointer
  Dim strLen As Integer
  Dim key As Pointer
  Dim keyLen As Integer
  Dim firstChild, lastChild, nextSibling, childCount As Integer
End Structure

Public Structure JsonDoc
  Dim nodes As Pointer
  Dim count, cap, root As Integer
  Dim ok As Boolean
  Dim errPos As Integer
  Dim errMsg As Pointer
End Structure

Public Structure JsonWriter
  Dim buf As Pointer
  Dim length, cap, depth As Integer
  Dim expectVal, overflow As Boolean
  Dim stackKind(64) As Byte
  Dim stackCount(64) As Integer
End Structure

Private Structure JsonParser
  Dim src As Pointer
  Dim srcLen, pos, depth, maxDepth As Integer
End Structure

' =============================================================================
' Internal Byte & Character Helpers
' =============================================================================

Private Function JsonByteAt(p As Pointer, idx As Integer) As Integer
  Dim b As Integer = p(idx)
  Return b And 255
End Function

Private Function JsonIsWs(c As Integer) As Boolean
  Return (c = 32) Or (c = 9) Or (c = 10) Or (c = 13)
End Function

Private Function JsonIsDigit(c As Integer) As Boolean
  Return (c >= 48) And (c <= 57)
End Function

Private Function JsonHexVal(c As Integer) As Integer
  If (c >= 48) And (c <= 57) Then
    Return c - 48
  End If
  If (c >= 65) And (c <= 70) Then
    Return c - 55
  End If
  If (c >= 97) And (c <= 102) Then
    Return c - 87
  End If
  Return -1
End Function

Private Function JsonHexChar(n As Integer) As Integer
  Dim d As Integer = n And 15
  If d < 10 Then
    Return 48 + d
  End If
  Return 87 + d
End Function

Private Function JsonStrEqBytes(a As Pointer, aLen As Integer, b As Pointer, bLen As Integer) As Boolean
  If aLen <> bLen Then
    Return False
  End If
  If aLen <= 0 Then
    Return True
  End If
  If IsNull(a) Or IsNull(b) Then
    Return False
  End If
  For i = 0 To aLen - 1 Do
    If JsonByteAt(a, i) <> JsonByteAt(b, i) Then
      Return False
    End If
  End For
  Return True
End Function

Private Function JsonCopyBytes(ByRef a As Arena, src As Pointer, n As Integer) As Pointer
  Dim dst As Pointer = ArenaAllocBytes(ByRef a, n + 1)
  If IsNull(dst) Then
    Return 0
  End If
  If (Not IsNull(src)) And (n > 0) Then
    For i = 0 To n - 1 Do
      dst(i) = src(i)
    End For
  End If
  dst(n) = 0
  Return dst
End Function

' =============================================================================
' Internal Node Pool Allocation & Tree Wiring
' =============================================================================

Private Function JsonAllocNode(ByRef doc As JsonDoc, kind As Integer) As Integer
  If IsNull(doc.nodes) Or (doc.count >= doc.cap) Then
    doc.ok = False
    doc.errMsg = "json node pool capacity exceeded"
    Return -1
  End If
  Dim id As Integer = doc.count
  doc.count = doc.count + 1
  Dim arr() As JsonNode = doc.nodes
  Dim empty As JsonNode With (
    kind = kind,
    boolVal = False,
    intVal = 0,
    floatVal = 0.0,
    strVal = 0,
    strLen = 0,
    key = 0,
    keyLen = 0,
    firstChild = -1,
    lastChild = -1,
    nextSibling = -1,
    childCount = 0
  )
  arr(id) = empty
  Return id
End Function

Private Sub JsonLinkChild(ByRef doc As JsonDoc, parentId As Integer, childId As Integer)
  Dim arr() As JsonNode = doc.nodes
  Dim prevLast As Integer = arr(parentId).lastChild
  If prevLast < 0 Then
    arr(parentId).firstChild = childId
    arr(parentId).lastChild = childId
  Else
    arr(prevLast).nextSibling = childId
    arr(parentId).lastChild = childId
  End If
  arr(parentId).childCount = arr(parentId).childCount + 1
End Sub

' =============================================================================
' Internal RFC 8259 Recursive-Descent Parser
' =============================================================================

Private Function JpPeek(ByRef p As JsonParser) As Integer
  If p.pos >= p.srcLen Then
    Return -1
  End If
  Return JsonByteAt(p.src, p.pos)
End Function

Private Sub JpSkipWs(ByRef p As JsonParser)
  While (p.pos < p.srcLen) And JsonIsWs(JsonByteAt(p.src, p.pos)) Do
    p.pos = p.pos + 1
  End While
End Sub

Private Function JpFail(ByRef doc As JsonDoc, ByRef p As JsonParser, msg As Pointer) As Integer
  If doc.ok Then
    doc.ok = False
    doc.errPos = p.pos
    doc.errMsg = msg
  End If
  Return -1
End Function

Private Function JpParseHex4(ByRef p As JsonParser, ByRef outCode As Integer) As Boolean
  If p.pos + 4 > p.srcLen Then
    Return False
  End If
  Dim v As Integer = 0
  For i = 0 To 3 Do
    Dim d As Integer = JsonHexVal(JsonByteAt(p.src, p.pos + i))
    If d < 0 Then
      Return False
    End If
    v = (v Shl 4) Or d
  End For
  p.pos = p.pos + 4
  outCode = v
  Return True
End Function

Private Function JpEncodeUtf8(dst As Pointer, outPos As Integer, cp As Integer) As Integer
  If cp <= 127 Then
    dst(outPos) = Byte(cp)
    Return outPos + 1
  End If
  If cp <= 2047 Then
    dst(outPos) = Byte(192 Or (cp Shr 6))
    dst(outPos + 1) = Byte(128 Or (cp And 63))
    Return outPos + 2
  End If
  If cp <= 65535 Then
    dst(outPos) = Byte(224 Or (cp Shr 12))
    dst(outPos + 1) = Byte(128 Or ((cp Shr 6) And 63))
    dst(outPos + 2) = Byte(128 Or (cp And 63))
    Return outPos + 3
  End If
  dst(outPos) = Byte(240 Or (cp Shr 18))
  dst(outPos + 1) = Byte(128 Or ((cp Shr 12) And 63))
  dst(outPos + 2) = Byte(128 Or ((cp Shr 6) And 63))
  dst(outPos + 3) = Byte(128 Or (cp And 63))
  Return outPos + 4
End Function

Private Function JpParseRawString(ByRef a As Arena, ByRef doc As JsonDoc, ByRef p As JsonParser, ByRef outPtr As Pointer, ByRef outLen As Integer) As Boolean
  If JpPeek(ByRef p) <> 34 Then
    JpFail(ByRef doc, ByRef p, "expected opening quote")
    Return False
  End If
  p.pos = p.pos + 1
  Dim startPos As Integer = p.pos

  ' Fast scan to find closing unescaped quote and verify raw bounds
  Dim scan As Integer = startPos
  Dim closed As Boolean = False
  While scan < p.srcLen Do
    Dim c As Integer = JsonByteAt(p.src, scan)
    If c = 34 Then
      closed = True
      Break
    End If
    If c < 32 Then
      p.pos = scan
      JpFail(ByRef doc, ByRef p, "unescaped control character in string")
      Return False
    End If
    If c = 92 Then
      scan = scan + 2
    Else
      scan = scan + 1
    End If
  End While

  If Not closed Then
    p.pos = p.srcLen
    JpFail(ByRef doc, ByRef p, "unterminated string")
    Return False
  End If

  Dim rawLen As Integer = scan - startPos
  Dim dst As Pointer = ArenaAllocBytes(ByRef a, rawLen + 1)
  If IsNull(dst) Then
    JpFail(ByRef doc, ByRef p, "arena out of memory")
    Return False
  End If

  Dim w As Integer = 0
  While p.pos < scan Do
    Dim c As Integer = JsonByteAt(p.src, p.pos)
    p.pos = p.pos + 1
    If c <> 92 Then
      dst(w) = Byte(c)
      w = w + 1
    Else
      If p.pos >= scan Then
        JpFail(ByRef doc, ByRef p, "incomplete escape sequence")
        Return False
      End If
      Dim esc As Integer = JsonByteAt(p.src, p.pos)
      p.pos = p.pos + 1
      Match esc With
        Case 34 Then
          dst(w) = 34
          w = w + 1
        Case 92 Then
          dst(w) = 92
          w = w + 1
        Case 47 Then
          dst(w) = 47
          w = w + 1
        Case 98 Then
          dst(w) = 8
          w = w + 1
        Case 102 Then
          dst(w) = 12
          w = w + 1
        Case 110 Then
          dst(w) = 10
          w = w + 1
        Case 114 Then
          dst(w) = 13
          w = w + 1
        Case 116 Then
          dst(w) = 9
          w = w + 1
        Case 117 Then
          Dim code As Integer = 0
          If Not JpParseHex4(ByRef p, ByRef code) Then
            JpFail(ByRef doc, ByRef p, "invalid unicode escape")
            Return False
          End If
          If (code >= 55296) And (code <= 56319) Then
            ' High surrogate: must be followed by \uXXXX low surrogate
            If (p.pos + 6 > scan) Or (JsonByteAt(p.src, p.pos) <> 92) Or (JsonByteAt(p.src, p.pos + 1) <> 117) Then
              JpFail(ByRef doc, ByRef p, "unpaired high surrogate")
              Return False
            End If
            p.pos = p.pos + 2
            Dim low As Integer = 0
            If Not JpParseHex4(ByRef p, ByRef low) Then
              JpFail(ByRef doc, ByRef p, "invalid low surrogate escape")
              Return False
            End If
            If (low < 56320) Or (low > 57343) Then
              JpFail(ByRef doc, ByRef p, "invalid low surrogate range")
              Return False
            End If
            Dim fullCp As Integer = 65536 + ((code - 55296) Shl 10) + (low - 56320)
            w = JpEncodeUtf8(dst, w, fullCp)
          Else
            If (code >= 56320) And (code <= 57343) Then
              JpFail(ByRef doc, ByRef p, "unpaired low surrogate")
              Return False
            End If
            w = JpEncodeUtf8(dst, w, code)
          End If
        Case _ Then
          JpFail(ByRef doc, ByRef p, "unknown escape sequence")
          Return False
      End Match
    End If
  End While

  ' Consume closing quote
  p.pos = scan + 1
  dst(w) = 0
  outPtr = dst
  outLen = w
  Return True
End Function

Private Function JpPow10(exp As Integer) As Double
  Dim e As Integer = exp
  Dim neg As Boolean = False
  If e < 0 Then
    neg = True
    e = 0 - e
  End If
  If e > 308 Then
    If neg Then
      Return 0.0
    End If
    Dim z As Double = 0.0
    Return 1.0 / z
  End If
  Dim res As Double = 1.0
  Dim base As Double = 10.0
  While e > 0 Do
    If (e And 1) = 1 Then
      res = res * base
    End If
    e = e Shr 1
    If e > 0 Then
      base = base * base
    End If
  End While
  If neg Then
    Return 1.0 / res
  End If
  Return res
End Function

Private Function JpParseNumber(ByRef doc As JsonDoc, ByRef p As JsonParser) As Integer
  Dim neg As Boolean = False
  If JpPeek(ByRef p) = 45 Then
    neg = True
    p.pos = p.pos + 1
  End If

  Dim first As Integer = JpPeek(ByRef p)
  If Not JsonIsDigit(first) Then
    Return JpFail(ByRef doc, ByRef p, "expected digit in number")
  End If

  ' Accumulate integer negatively so INT64_MIN (-9223372036854775808) fits cleanly
  Dim minLong As Long = Long(1) Shl 63
  Dim intAcc As Long = 0
  Dim intOverflow As Boolean = False
  Dim dblAcc As Double = 0.0

  If first = 48 Then
    p.pos = p.pos + 1
    If JsonIsDigit(JpPeek(ByRef p)) Then
      Return JpFail(ByRef doc, ByRef p, "leading zeros not allowed in JSON number")
    End If
  Else
    While JsonIsDigit(JpPeek(ByRef p)) Do
      Dim d As Integer = JpPeek(ByRef p) - 48
      p.pos = p.pos + 1
      dblAcc = dblAcc * 10.0 + Double(d)
      If Not intOverflow Then
        Dim limit As Long = (minLong + Long(d)) / 10
        If intAcc < limit Then
          intOverflow = True
        Else
          intAcc = intAcc * 10 - Long(d)
        End If
      End If
    End While
  End If

  Dim isFloat As Boolean = False
  Dim fracScale As Integer = 0

  ' Optional fractional part
  If JpPeek(ByRef p) = 46 Then
    isFloat = True
    p.pos = p.pos + 1
    If Not JsonIsDigit(JpPeek(ByRef p)) Then
      Return JpFail(ByRef doc, ByRef p, "expected digit after decimal point")
    End If
    While JsonIsDigit(JpPeek(ByRef p)) Do
      Dim d As Integer = JpPeek(ByRef p) - 48
      p.pos = p.pos + 1
      If fracScale < 300 Then
        dblAcc = dblAcc * 10.0 + Double(d)
        fracScale = fracScale + 1
      End If
    End While
  End If

  ' Optional exponent part
  Dim expVal As Integer = 0
  Dim ec As Integer = JpPeek(ByRef p)
  If (ec = 101) Or (ec = 69) Then
    isFloat = True
    p.pos = p.pos + 1
    Dim expNeg As Boolean = False
    Dim sc As Integer = JpPeek(ByRef p)
    If sc = 45 Then
      expNeg = True
      p.pos = p.pos + 1
    Else
      If sc = 43 Then
        p.pos = p.pos + 1
      End If
    End If
    If Not JsonIsDigit(JpPeek(ByRef p)) Then
      Return JpFail(ByRef doc, ByRef p, "expected digit in exponent")
    End If
    While JsonIsDigit(JpPeek(ByRef p)) Do
      Dim d As Integer = JpPeek(ByRef p) - 48
      p.pos = p.pos + 1
      If expVal < 10000 Then
        expVal = expVal * 10 + d
      End If
    End While
    If expNeg Then
      expVal = 0 - expVal
    End If
  End If

  If (Not isFloat) And (Not intOverflow) Then
    If (Not neg) And (intAcc = minLong) Then
      intOverflow = True
    End If
  End If

  If (Not isFloat) And (Not intOverflow) Then
    Dim id As Integer = JsonAllocNode(ByRef doc, JsonInt)
    If id < 0 Then
      Return -1
    End If
    Dim val As Long = intAcc
    If Not neg Then
      val = 0 - intAcc
    End If
    Dim arr() As JsonNode = doc.nodes
    arr(id).intVal = val
    arr(id).floatVal = Double(val)
    Return id
  End If

  ' Floating-point value
  Dim netExp As Integer = expVal - fracScale
  Dim fVal As Double = dblAcc
  If fVal <> 0.0 Then
    If netExp < -308 Then
      ' Two-step scaling avoids subnormal underflow via 1/1e308
      fVal = fVal * JpPow10(-300)
      Dim remExp As Integer = netExp + 300
      If remExp < -308 Then
        fVal = 0.0
      Else
        fVal = fVal * JpPow10(remExp)
      End If
    Else
      fVal = fVal * JpPow10(netExp)
    End If
  End If
  If neg Then
    fVal = 0.0 - fVal
  End If

  Dim fid As Integer = JsonAllocNode(ByRef doc, JsonFloat)
  If fid < 0 Then
    Return -1
  End If
  Dim farr() As JsonNode = doc.nodes
  farr(fid).floatVal = fVal
  farr(fid).intVal = Long(fVal)
  Return fid
End Function

Private Function JpMatchLit(ByRef p As JsonParser, lit As Pointer, n As Integer) As Boolean
  If p.pos + n > p.srcLen Then
    Return False
  End If
  For i = 0 To n - 1 Do
    If JsonByteAt(p.src, p.pos + i) <> JsonByteAt(lit, i) Then
      Return False
    End If
  End For
  p.pos = p.pos + n
  Return True
End Function

Private Function JpParseValue(ByRef a As Arena, ByRef doc As JsonDoc, ByRef p As JsonParser) As Integer
  JpSkipWs(ByRef p)
  Dim c As Integer = JpPeek(ByRef p)
  If c < 0 Then
    Return JpFail(ByRef doc, ByRef p, "unexpected end of JSON input")
  End If

  ' String
  If c = 34 Then
    Dim sp As Pointer = 0
    Dim sl As Integer = 0
    If Not JpParseRawString(ByRef a, ByRef doc, ByRef p, ByRef sp, ByRef sl) Then
      Return -1
    End If
    Dim sid As Integer = JsonAllocNode(ByRef doc, JsonString)
    If sid < 0 Then
      Return -1
    End If
    Dim sarr() As JsonNode = doc.nodes
    sarr(sid).strVal = sp
    sarr(sid).strLen = sl
    Return sid
  End If

  ' Object
  If c = 123 Then
    If p.depth >= p.maxDepth Then
      Return JpFail(ByRef doc, ByRef p, "maximum JSON nesting depth exceeded")
    End If
    p.pos = p.pos + 1
    p.depth = p.depth + 1
    Dim oid As Integer = JsonAllocNode(ByRef doc, JsonObject)
    If oid < 0 Then
      Return -1
    End If
    JpSkipWs(ByRef p)
    If JpPeek(ByRef p) = 125 Then
      p.pos = p.pos + 1
      p.depth = p.depth - 1
      Return oid
    End If
    While True Do
      JpSkipWs(ByRef p)
      Dim kp As Pointer = 0
      Dim kl As Integer = 0
      If Not JpParseRawString(ByRef a, ByRef doc, ByRef p, ByRef kp, ByRef kl) Then
        Return -1
      End If
      JpSkipWs(ByRef p)
      If JpPeek(ByRef p) <> 58 Then
        Return JpFail(ByRef doc, ByRef p, "expected ':' after object key")
      End If
      p.pos = p.pos + 1
      Dim vid As Integer = JpParseValue(ByRef a, ByRef doc, ByRef p)
      If vid < 0 Then
        Return -1
      End If
      Dim arr() As JsonNode = doc.nodes
      arr(vid).key = kp
      arr(vid).keyLen = kl
      JsonLinkChild(ByRef doc, oid, vid)
      JpSkipWs(ByRef p)
      Dim sep As Integer = JpPeek(ByRef p)
      If sep = 44 Then
        p.pos = p.pos + 1
      Else
        If sep = 125 Then
          p.pos = p.pos + 1
          p.depth = p.depth - 1
          Return oid
        End If
        Return JpFail(ByRef doc, ByRef p, "expected ',' or '}' in object")
      End If
    End While
  End If

  ' Array
  If c = 91 Then
    If p.depth >= p.maxDepth Then
      Return JpFail(ByRef doc, ByRef p, "maximum JSON nesting depth exceeded")
    End If
    p.pos = p.pos + 1
    p.depth = p.depth + 1
    Dim aid As Integer = JsonAllocNode(ByRef doc, JsonArray)
    If aid < 0 Then
      Return -1
    End If
    JpSkipWs(ByRef p)
    If JpPeek(ByRef p) = 93 Then
      p.pos = p.pos + 1
      p.depth = p.depth - 1
      Return aid
    End If
    While True Do
      Dim eid As Integer = JpParseValue(ByRef a, ByRef doc, ByRef p)
      If eid < 0 Then
        Return -1
      End If
      JsonLinkChild(ByRef doc, aid, eid)
      JpSkipWs(ByRef p)
      Dim sep As Integer = JpPeek(ByRef p)
      If sep = 44 Then
        p.pos = p.pos + 1
      Else
        If sep = 93 Then
          p.pos = p.pos + 1
          p.depth = p.depth - 1
          Return aid
        End If
        Return JpFail(ByRef doc, ByRef p, "expected ',' or ']' in array")
      End If
    End While
  End If

  ' true
  If c = 116 Then
    If Not JpMatchLit(ByRef p, "true", 4) Then
      Return JpFail(ByRef doc, ByRef p, "invalid literal (expected 'true')")
    End If
    Dim tid As Integer = JsonAllocNode(ByRef doc, JsonBool)
    If tid < 0 Then
      Return -1
    End If
    Dim tarr() As JsonNode = doc.nodes
    tarr(tid).boolVal = True
    tarr(tid).intVal = 1
    Return tid
  End If

  ' false
  If c = 102 Then
    If Not JpMatchLit(ByRef p, "false", 5) Then
      Return JpFail(ByRef doc, ByRef p, "invalid literal (expected 'false')")
    End If
    Dim fid As Integer = JsonAllocNode(ByRef doc, JsonBool)
    If fid < 0 Then
      Return -1
    End If
    Return fid
  End If

  ' null
  If c = 110 Then
    If Not JpMatchLit(ByRef p, "null", 4) Then
      Return JpFail(ByRef doc, ByRef p, "invalid literal (expected 'null')")
    End If
    Return JsonAllocNode(ByRef doc, JsonNull)
  End If

  ' Number
  If (c = 45) Or JsonIsDigit(c) Then
    Return JpParseNumber(ByRef doc, ByRef p)
  End If

  Return JpFail(ByRef doc, ByRef p, "unexpected character in JSON")
End Function

' =============================================================================
' Public Parse & Document Lifecycle API
' =============================================================================

Public Function JsonDocNew(ByRef a As Arena, maxNodes As Integer) As JsonDoc
  Dim doc As JsonDoc With (
    nodes = 0,
    count = 0,
    cap = 0,
    root = -1,
    ok = False,
    errPos = 0,
    errMsg = ""
  )
  Dim cap As Integer = maxNodes
  If cap < 4 Then
    cap = 4
  End If
  Dim pool() As JsonNode = ArenaAlloc(ByRef a, JsonNode, cap)
  If IsNull(pool) Then
    doc.errMsg = "arena out of memory"
    Return doc
  End If
  doc.nodes = pool
  doc.cap = cap
  doc.ok = True
  Return doc
End Function

Public Function JsonParseWithCap(ByRef a As Arena, src As Pointer, maxNodes As Integer) As JsonDoc
  Dim doc As JsonDoc = JsonDocNew(ByRef a, maxNodes)
  If Not doc.ok Then
    Return doc
  End If
  If IsNull(src) Then
    doc.ok = False
    doc.errMsg = "null JSON input"
    Return doc
  End If
  Dim p As JsonParser With (
    src = src,
    srcLen = StrLen(src),
    pos = 0,
    depth = 0,
    maxDepth = 64
  )
  Dim rootId As Integer = JpParseValue(ByRef a, ByRef doc, ByRef p)
  If (rootId < 0) Or (Not doc.ok) Then
    doc.ok = False
    doc.root = -1
    Return doc
  End If
  JpSkipWs(ByRef p)
  If p.pos <> p.srcLen Then
    JpFail(ByRef doc, ByRef p, "trailing data after JSON root value")
    doc.root = -1
    Return doc
  End If
  doc.root = rootId
  Return doc
End Function

Public Function JsonParse(ByRef a As Arena, src As Pointer) As JsonDoc
  Dim n As Integer = StrLen(src)
  Dim est As Integer = (n / 2) + 8
  If est < 16 Then
    est = 16
  End If
  Return JsonParseWithCap(ByRef a, src, est)
End Function

' =============================================================================
' Public DOM Inspection & Typed Accessor API
' =============================================================================

Public Function JsonIsValid(ByRef doc As JsonDoc, id As Integer) As Boolean
  If IsNull(doc.nodes) Then
    Return False
  End If
  Return (id >= 0) And (id < doc.count)
End Function

Public Function JsonKindOf(ByRef doc As JsonDoc, id As Integer) As Integer
  If Not JsonIsValid(ByRef doc, id) Then
    Return JsonInvalid
  End If
  Dim arr() As JsonNode = doc.nodes
  Return arr(id).kind
End Function

Public Function JsonIsNull(ByRef doc As JsonDoc, id As Integer) As Boolean
  Return JsonKindOf(ByRef doc, id) = JsonNull
End Function

Public Function JsonIsBool(ByRef doc As JsonDoc, id As Integer) As Boolean
  Return JsonKindOf(ByRef doc, id) = JsonBool
End Function

Public Function JsonIsInt(ByRef doc As JsonDoc, id As Integer) As Boolean
  Return JsonKindOf(ByRef doc, id) = JsonInt
End Function

Public Function JsonIsFloat(ByRef doc As JsonDoc, id As Integer) As Boolean
  Return JsonKindOf(ByRef doc, id) = JsonFloat
End Function

Public Function JsonIsNumber(ByRef doc As JsonDoc, id As Integer) As Boolean
  Dim k As Integer = JsonKindOf(ByRef doc, id)
  Return (k = JsonInt) Or (k = JsonFloat)
End Function

Public Function JsonIsString(ByRef doc As JsonDoc, id As Integer) As Boolean
  Return JsonKindOf(ByRef doc, id) = JsonString
End Function

Public Function JsonIsArray(ByRef doc As JsonDoc, id As Integer) As Boolean
  Return JsonKindOf(ByRef doc, id) = JsonArray
End Function

Public Function JsonIsObject(ByRef doc As JsonDoc, id As Integer) As Boolean
  Return JsonKindOf(ByRef doc, id) = JsonObject
End Function

Public Function JsonAsBool(ByRef doc As JsonDoc, id As Integer, fallback As Boolean) As Boolean
  If JsonKindOf(ByRef doc, id) <> JsonBool Then
    Return fallback
  End If
  Dim arr() As JsonNode = doc.nodes
  Return arr(id).boolVal
End Function

Public Function JsonAsLong(ByRef doc As JsonDoc, id As Integer, fallback As Long) As Long
  Dim k As Integer = JsonKindOf(ByRef doc, id)
  If (k <> JsonInt) And (k <> JsonFloat) Then
    Return fallback
  End If
  Dim arr() As JsonNode = doc.nodes
  Return arr(id).intVal
End Function

Public Function JsonAsInt(ByRef doc As JsonDoc, id As Integer, fallback As Integer) As Integer
  Dim k As Integer = JsonKindOf(ByRef doc, id)
  If (k <> JsonInt) And (k <> JsonFloat) Then
    Return fallback
  End If
  Dim arr() As JsonNode = doc.nodes
  Return Integer(arr(id).intVal)
End Function

Public Function JsonAsDouble(ByRef doc As JsonDoc, id As Integer, fallback As Double) As Double
  Dim k As Integer = JsonKindOf(ByRef doc, id)
  If (k <> JsonInt) And (k <> JsonFloat) Then
    Return fallback
  End If
  Dim arr() As JsonNode = doc.nodes
  Return arr(id).floatVal
End Function

Public Function JsonAsString(ByRef doc As JsonDoc, id As Integer, fallback As Pointer) As Pointer
  If JsonKindOf(ByRef doc, id) <> JsonString Then
    Return fallback
  End If
  Dim arr() As JsonNode = doc.nodes
  Return arr(id).strVal
End Function

Public Function JsonStrLen(ByRef doc As JsonDoc, id As Integer) As Integer
  If JsonKindOf(ByRef doc, id) <> JsonString Then
    Return 0
  End If
  Dim arr() As JsonNode = doc.nodes
  Return arr(id).strLen
End Function

Public Function JsonLen(ByRef doc As JsonDoc, id As Integer) As Integer
  Dim k As Integer = JsonKindOf(ByRef doc, id)
  If (k <> JsonArray) And (k <> JsonObject) Then
    Return 0
  End If
  Dim arr() As JsonNode = doc.nodes
  Return arr(id).childCount
End Function

Public Function JsonFirst(ByRef doc As JsonDoc, parentId As Integer) As Integer
  Dim k As Integer = JsonKindOf(ByRef doc, parentId)
  If (k <> JsonArray) And (k <> JsonObject) Then
    Return -1
  End If
  Dim arr() As JsonNode = doc.nodes
  Return arr(parentId).firstChild
End Function

Public Function JsonNext(ByRef doc As JsonDoc, nodeId As Integer) As Integer
  If Not JsonIsValid(ByRef doc, nodeId) Then
    Return -1
  End If
  Dim arr() As JsonNode = doc.nodes
  Return arr(nodeId).nextSibling
End Function

Public Function JsonKey(ByRef doc As JsonDoc, nodeId As Integer) As Pointer
  If Not JsonIsValid(ByRef doc, nodeId) Then
    Return 0
  End If
  Dim arr() As JsonNode = doc.nodes
  Return arr(nodeId).key
End Function

Public Function JsonAt(ByRef doc As JsonDoc, arrId As Integer, idx As Integer) As Integer
  If (JsonKindOf(ByRef doc, arrId) <> JsonArray) Or (idx < 0) Then
    Return -1
  End If
  Dim arr() As JsonNode = doc.nodes
  If idx >= arr(arrId).childCount Then
    Return -1
  End If
  Dim cur As Integer = arr(arrId).firstChild
  Dim i As Integer = 0
  While (cur >= 0) And (i < idx) Do
    cur = arr(cur).nextSibling
    i = i + 1
  End While
  Return cur
End Function

' Object lookup (last matching key wins, matching RFC 8259 / Python / JS convention)
Public Function JsonGet(ByRef doc As JsonDoc, objId As Integer, key As Pointer) As Integer
  If (JsonKindOf(ByRef doc, objId) <> JsonObject) Or IsNull(key) Then
    Return -1
  End If
  Dim kLen As Integer = StrLen(key)
  Dim arr() As JsonNode = doc.nodes
  Dim cur As Integer = arr(objId).firstChild
  Dim found As Integer = -1
  While cur >= 0 Do
    If JsonStrEqBytes(arr(cur).key, arr(cur).keyLen, key, kLen) Then
      found = cur
    End If
    cur = arr(cur).nextSibling
  End While
  Return found
End Function

Public Function JsonHas(ByRef doc As JsonDoc, objId As Integer, key As Pointer) As Boolean
  Return JsonGet(ByRef doc, objId, key) >= 0
End Function

Public Function JsonGetString(ByRef doc As JsonDoc, objId As Integer, key As Pointer, fallback As Pointer) As Pointer
  Return JsonAsString(ByRef doc, JsonGet(ByRef doc, objId, key), fallback)
End Function

Public Function JsonGetInt(ByRef doc As JsonDoc, objId As Integer, key As Pointer, fallback As Integer) As Integer
  Return JsonAsInt(ByRef doc, JsonGet(ByRef doc, objId, key), fallback)
End Function

Public Function JsonGetLong(ByRef doc As JsonDoc, objId As Integer, key As Pointer, fallback As Long) As Long
  Return JsonAsLong(ByRef doc, JsonGet(ByRef doc, objId, key), fallback)
End Function

Public Function JsonGetDouble(ByRef doc As JsonDoc, objId As Integer, key As Pointer, fallback As Double) As Double
  Return JsonAsDouble(ByRef doc, JsonGet(ByRef doc, objId, key), fallback)
End Function

Public Function JsonGetBool(ByRef doc As JsonDoc, objId As Integer, key As Pointer, fallback As Boolean) As Boolean
  Return JsonAsBool(ByRef doc, JsonGet(ByRef doc, objId, key), fallback)
End Function

' =============================================================================
' Public DOM Construction API
' =============================================================================

Public Function JsonNewNull(ByRef doc As JsonDoc) As Integer
  Return JsonAllocNode(ByRef doc, JsonNull)
End Function

Public Function JsonNewBool(ByRef doc As JsonDoc, val As Boolean) As Integer
  Dim id As Integer = JsonAllocNode(ByRef doc, JsonBool)
  If id < 0 Then
    Return -1
  End If
  Dim arr() As JsonNode = doc.nodes
  arr(id).boolVal = val
  If val Then
    arr(id).intVal = 1
  End If
  Return id
End Function

Public Function JsonNewInt(ByRef doc As JsonDoc, val As Long) As Integer
  Dim id As Integer = JsonAllocNode(ByRef doc, JsonInt)
  If id < 0 Then
    Return -1
  End If
  Dim arr() As JsonNode = doc.nodes
  arr(id).intVal = val
  arr(id).floatVal = Double(val)
  Return id
End Function

Public Function JsonNewDouble(ByRef doc As JsonDoc, val As Double) As Integer
  Dim id As Integer = JsonAllocNode(ByRef doc, JsonFloat)
  If id < 0 Then
    Return -1
  End If
  Dim arr() As JsonNode = doc.nodes
  arr(id).floatVal = val
  arr(id).intVal = Long(val)
  Return id
End Function

Public Function JsonNewString(ByRef a As Arena, ByRef doc As JsonDoc, s As Pointer) As Integer
  Dim id As Integer = JsonAllocNode(ByRef doc, JsonString)
  If id < 0 Then
    Return -1
  End If
  Dim n As Integer = StrLen(s)
  Dim cp As Pointer = JsonCopyBytes(ByRef a, s, n)
  If IsNull(cp) Then
    doc.ok = False
    doc.errMsg = "arena out of memory"
    Return -1
  End If
  Dim arr() As JsonNode = doc.nodes
  arr(id).strVal = cp
  arr(id).strLen = n
  Return id
End Function

Public Function JsonNewArray(ByRef doc As JsonDoc) As Integer
  Return JsonAllocNode(ByRef doc, JsonArray)
End Function

Public Function JsonNewObject(ByRef doc As JsonDoc) As Integer
  Return JsonAllocNode(ByRef doc, JsonObject)
End Function

Public Function JsonArrayPush(ByRef doc As JsonDoc, arrId As Integer, elemId As Integer) As Boolean
  If (JsonKindOf(ByRef doc, arrId) <> JsonArray) Or (Not JsonIsValid(ByRef doc, elemId)) Then
    Return False
  End If
  JsonLinkChild(ByRef doc, arrId, elemId)
  Return True
End Function

Public Function JsonObjectSet(ByRef a As Arena, ByRef doc As JsonDoc, objId As Integer, key As Pointer, valId As Integer) As Boolean
  If (JsonKindOf(ByRef doc, objId) <> JsonObject) Or IsNull(key) Or (Not JsonIsValid(ByRef doc, valId)) Then
    Return False
  End If
  Dim kLen As Integer = StrLen(key)
  Dim kCopy As Pointer = JsonCopyBytes(ByRef a, key, kLen)
  If IsNull(kCopy) Then
    doc.ok = False
    doc.errMsg = "arena out of memory"
    Return False
  End If
  Dim arr() As JsonNode = doc.nodes
  arr(valId).key = kCopy
  arr(valId).keyLen = kLen
  JsonLinkChild(ByRef doc, objId, valId)
  Return True
End Function

' =============================================================================
' Streaming JsonWriter (Zero-DOM, Arena-backed, Auto-Comma & Escaping)
' =============================================================================

Public Function JwNew(ByRef a As Arena, cap As Integer) As JsonWriter
  Dim c As Integer = cap
  If c < 1 Then
    c = 1
  End If
  Dim buf As Pointer = ArenaAllocBytes(ByRef a, c)
  Dim w As JsonWriter
  w.buf = buf
  w.length = 0
  w.cap = 0
  w.depth = 0
  w.expectVal = False
  w.overflow = False
  If Not IsNull(buf) Then
    w.cap = c
    buf(0) = 0
  Else
    w.overflow = True
  End If
  Return w
End Function

Private Sub JwPutByte(ByRef w As JsonWriter, ch As Integer)
  If IsNull(w.buf) Then
    w.overflow = True
    Return
  End If
  If w.length + 1 >= w.cap Then
    w.overflow = True
    Return
  End If
  w.buf(w.length) = Byte(ch And 255)
  w.length = w.length + 1
  w.buf(w.length) = 0
End Sub

Private Sub JwPutBytes(ByRef w As JsonWriter, s As Pointer, n As Integer)
  If IsNull(s) Or (n <= 0) Then
    Return
  End If
  For i = 0 To n - 1 Do
    JwPutByte(ByRef w, JsonByteAt(s, i))
  End For
End Sub

Private Sub JwPutStr(ByRef w As JsonWriter, s As Pointer)
  If IsNull(s) Then
    Return
  End If
  JwPutBytes(ByRef w, s, StrLen(s))
End Sub

Private Sub JwPutEscapedBytes(ByRef w As JsonWriter, s As Pointer, n As Integer)
  JwPutByte(ByRef w, 34)
  If (Not IsNull(s)) And (n > 0) Then
    For i = 0 To n - 1 Do
      Dim c As Integer = JsonByteAt(s, i)
      Match c With
        Case 34 Then
          JwPutByte(ByRef w, 92)
          JwPutByte(ByRef w, 34)
        Case 92 Then
          JwPutByte(ByRef w, 92)
          JwPutByte(ByRef w, 92)
        Case 8 Then
          JwPutByte(ByRef w, 92)
          JwPutByte(ByRef w, 98)
        Case 12 Then
          JwPutByte(ByRef w, 92)
          JwPutByte(ByRef w, 102)
        Case 10 Then
          JwPutByte(ByRef w, 92)
          JwPutByte(ByRef w, 110)
        Case 13 Then
          JwPutByte(ByRef w, 92)
          JwPutByte(ByRef w, 114)
        Case 9 Then
          JwPutByte(ByRef w, 92)
          JwPutByte(ByRef w, 116)
        Case _ Then
          If c < 32 Then
            JwPutStr(ByRef w, "\\u00")
            JwPutByte(ByRef w, JsonHexChar(c Shr 4))
            JwPutByte(ByRef w, JsonHexChar(c))
          Else
            JwPutByte(ByRef w, c)
          End If
      End Match
    End For
  End If
  JwPutByte(ByRef w, 34)
End Sub

Private Sub JwBeforeValue(ByRef w As JsonWriter)
  If w.expectVal Then
    w.expectVal = False
    Return
  End If
  If w.depth > 0 Then
    Dim top As Integer = w.depth - 1
    If w.stackCount(top) > 0 Then
      JwPutByte(ByRef w, 44)
    End If
    w.stackCount(top) = w.stackCount(top) + 1
  End If
End Sub

Private Sub JwPutLongRaw(ByRef w As JsonWriter, n As Long)
  If n = 0 Then
    JwPutByte(ByRef w, 48)
    Return
  End If
  Dim v As Long = n
  If v < 0 Then
    JwPutByte(ByRef w, 45)
  Else
    v = 0 - v
  End If
  Dim rev(32) As Byte
  Dim cnt As Integer = 0
  While v < 0 Do
    Dim digit As Integer = Integer(0 - (v Mod 10))
    rev(cnt) = Byte(48 + digit)
    cnt = cnt + 1
    v = v / 10
  End While
  For i = 0 To cnt - 1 Do
    JwPutByte(ByRef w, rev(cnt - 1 - i) And 255)
  End For
End Sub

Private Sub JwPutDoubleRaw(ByRef w As JsonWriter, val As Double)
  ' Reject NaN and +/-Inf per RFC 8259 (ordered == 0.0 is true iff val is finite)
  If Not ((val - val) = 0.0) Then
    JwPutStr(ByRef w, "null")
    Return
  End If
  If val = 0.0 Then
    JwPutStr(ByRef w, "0.0")
    Return
  End If
  Dim x As Double = val
  If x < 0.0 Then
    JwPutByte(ByRef w, 45)
    x = 0.0 - x
  End If

  If (x >= 1000000000000000.0) Or (x < 0.0001) Then
    ' Scientific notation: normalize x into [1.0, 10.0)
    Dim exp As Integer = 0
    While x >= 10.0 Do
      x = x / 10.0
      exp = exp + 1
    End While
    While x < 1.0 Do
      x = x * 10.0
      exp = exp - 1
    End While
    Dim lead As Integer = Integer(x)
    If lead > 9 Then
      lead = 9
    End If
    JwPutByte(ByRef w, 48 + lead)
    Dim rem As Double = x - Double(lead)
    Dim digits(16) As Byte
    Dim lastNonZero As Integer = -1
    For i = 0 To 11 Do
      rem = rem * 10.0
      Dim d As Integer = Integer(rem)
      If d < 0 Then
        d = 0
      End If
      If d > 9 Then
        d = 9
      End If
      digits(i) = Byte(48 + d)
      If d <> 0 Then
        lastNonZero = i
      End If
      rem = rem - Double(d)
    End For
    If lastNonZero >= 0 Then
      JwPutByte(ByRef w, 46)
      For i = 0 To lastNonZero Do
        JwPutByte(ByRef w, digits(i) And 255)
      End For
    End If
    JwPutByte(ByRef w, 101)
    If exp >= 0 Then
      JwPutByte(ByRef w, 43)
    End If
    JwPutLongRaw(ByRef w, Long(exp))
    Return
  End If

  ' Fixed-point decimal formatting
  Dim intPart As Long = Long(x)
  Dim frac As Double = x - Double(intPart)
  Dim scale As Double = 1000000000000.0
  Dim ten12 As Long = Long(1000000) * Long(1000000)
  Dim fracScaled As Long = Long(frac * scale + 0.5)
  If fracScaled >= ten12 Then
    intPart = intPart + 1
    fracScaled = 0
  End If
  JwPutLongRaw(ByRef w, intPart)
  JwPutByte(ByRef w, 46)
  If fracScaled = 0 Then
    JwPutByte(ByRef w, 48)
    Return
  End If
  Dim fbuf(16) As Byte
  Dim tmp As Long = fracScaled
  For i = 0 To 11 Do
    Dim d As Integer = Integer(tmp Mod 10)
    fbuf(11 - i) = Byte(48 + d)
    tmp = tmp / 10
  End For
  Dim lastNz As Integer = 11
  While (lastNz > 0) And (fbuf(lastNz) = 48) Do
    lastNz = lastNz - 1
  End While
  For i = 0 To lastNz Do
    JwPutByte(ByRef w, fbuf(i) And 255)
  End For
End Sub

Public Sub JwBeginObj(ByRef w As JsonWriter)
  JwBeforeValue(ByRef w)
  JwPutByte(ByRef w, 123)
  If w.depth < 64 Then
    w.stackKind(w.depth) = 1
    w.stackCount(w.depth) = 0
    w.depth = w.depth + 1
  Else
    w.overflow = True
  End If
End Sub

Public Sub JwEndObj(ByRef w As JsonWriter)
  If w.depth > 0 Then
    w.depth = w.depth - 1
  End If
  w.expectVal = False
  JwPutByte(ByRef w, 125)
End Sub

Public Sub JwBeginArr(ByRef w As JsonWriter)
  JwBeforeValue(ByRef w)
  JwPutByte(ByRef w, 91)
  If w.depth < 64 Then
    w.stackKind(w.depth) = 0
    w.stackCount(w.depth) = 0
    w.depth = w.depth + 1
  Else
    w.overflow = True
  End If
End Sub

Public Sub JwEndArr(ByRef w As JsonWriter)
  If w.depth > 0 Then
    w.depth = w.depth - 1
  End If
  w.expectVal = False
  JwPutByte(ByRef w, 93)
End Sub

Public Sub JwKey(ByRef w As JsonWriter, key As Pointer)
  If w.depth > 0 Then
    Dim top As Integer = w.depth - 1
    If w.stackCount(top) > 0 Then
      JwPutByte(ByRef w, 44)
    End If
    w.stackCount(top) = w.stackCount(top) + 1
  End If
  JwPutEscapedBytes(ByRef w, key, StrLen(key))
  JwPutByte(ByRef w, 58)
  w.expectVal = True
End Sub

Public Sub JwNull(ByRef w As JsonWriter)
  JwBeforeValue(ByRef w)
  JwPutStr(ByRef w, "null")
End Sub

Public Sub JwBool(ByRef w As JsonWriter, val As Boolean)
  JwBeforeValue(ByRef w)
  If val Then
    JwPutStr(ByRef w, "true")
  Else
    JwPutStr(ByRef w, "false")
  End If
End Sub

Public Sub JwInt(ByRef w As JsonWriter, val As Integer)
  JwBeforeValue(ByRef w)
  JwPutLongRaw(ByRef w, Long(val))
End Sub

Public Sub JwLong(ByRef w As JsonWriter, val As Long)
  JwBeforeValue(ByRef w)
  JwPutLongRaw(ByRef w, val)
End Sub

Public Sub JwDouble(ByRef w As JsonWriter, val As Double)
  JwBeforeValue(ByRef w)
  JwPutDoubleRaw(ByRef w, val)
End Sub

Public Sub JwString(ByRef w As JsonWriter, s As Pointer)
  JwBeforeValue(ByRef w)
  JwPutEscapedBytes(ByRef w, s, StrLen(s))
End Sub

Public Sub JwStringBytes(ByRef w As JsonWriter, s As Pointer, n As Integer)
  JwBeforeValue(ByRef w)
  JwPutEscapedBytes(ByRef w, s, n)
End Sub

Public Sub JwRaw(ByRef w As JsonWriter, rawJson As Pointer)
  JwBeforeValue(ByRef w)
  JwPutStr(ByRef w, rawJson)
End Sub

Public Sub JwFieldNull(ByRef w As JsonWriter, key As Pointer)
  JwKey(ByRef w, key)
  JwNull(ByRef w)
End Sub

Public Sub JwFieldBool(ByRef w As JsonWriter, key As Pointer, val As Boolean)
  JwKey(ByRef w, key)
  JwBool(ByRef w, val)
End Sub

Public Sub JwFieldInt(ByRef w As JsonWriter, key As Pointer, val As Integer)
  JwKey(ByRef w, key)
  JwInt(ByRef w, val)
End Sub

Public Sub JwFieldLong(ByRef w As JsonWriter, key As Pointer, val As Long)
  JwKey(ByRef w, key)
  JwLong(ByRef w, val)
End Sub

Public Sub JwFieldDouble(ByRef w As JsonWriter, key As Pointer, val As Double)
  JwKey(ByRef w, key)
  JwDouble(ByRef w, val)
End Sub

Public Sub JwFieldString(ByRef w As JsonWriter, key As Pointer, val As Pointer)
  JwKey(ByRef w, key)
  JwString(ByRef w, val)
End Sub

Public Sub JwFieldRaw(ByRef w As JsonWriter, key As Pointer, rawJson As Pointer)
  JwKey(ByRef w, key)
  JwRaw(ByRef w, rawJson)
End Sub

Public Function JwFinish(ByRef w As JsonWriter) As Pointer
  If IsNull(w.buf) Then
    Return 0
  End If
  w.buf(w.length) = 0
  Return w.buf
End Function

' =============================================================================
' DOM Serialization (JsonStringify)
' =============================================================================

Private Function JsonEscapedByteLen(s As Pointer, n As Integer) As Integer
  Dim total As Integer = 2
  If (Not IsNull(s)) And (n > 0) Then
    For i = 0 To n - 1 Do
      Dim c As Integer = JsonByteAt(s, i)
      If (c = 34) Or (c = 92) Or (c = 8) Or (c = 12) Or (c = 10) Or (c = 13) Or (c = 9) Then
        total = total + 2
      Else
        If c < 32 Then
          total = total + 6
        Else
          total = total + 1
        End If
      End If
    End For
  End If
  Return total
End Function

Private Function JsonMeasureNode(ByRef doc As JsonDoc, id As Integer, depth As Integer) As Integer
  If (Not JsonIsValid(ByRef doc, id)) Or (depth > 64) Then
    Return 4
  End If
  Dim arr() As JsonNode = doc.nodes
  Dim k As Integer = arr(id).kind
  Match k With
    Case JsonNull Then
      Return 4
    Case JsonBool Then
      Return 5
    Case JsonInt Then
      Return 24
    Case JsonFloat Then
      Return 32
    Case JsonString Then
      Return JsonEscapedByteLen(arr(id).strVal, arr(id).strLen)
    Case JsonArray Then
      Dim total As Integer = 2
      Dim cur As Integer = arr(id).firstChild
      While cur >= 0 Do
        total = total + JsonMeasureNode(ByRef doc, cur, depth + 1) + 1
        cur = arr(cur).nextSibling
      End While
      Return total
    Case JsonObject Then
      Dim total As Integer = 2
      Dim cur As Integer = arr(id).firstChild
      While cur >= 0 Do
        total = total + JsonEscapedByteLen(arr(cur).key, arr(cur).keyLen) + 1
        total = total + JsonMeasureNode(ByRef doc, cur, depth + 1) + 1
        cur = arr(cur).nextSibling
      End While
      Return total
    Case _ Then
      Return 4
  End Match
End Function

Private Sub JsonWriteNode(ByRef w As JsonWriter, ByRef doc As JsonDoc, id As Integer, depth As Integer)
  If (Not JsonIsValid(ByRef doc, id)) Or (depth > 64) Then
    JwNull(ByRef w)
    Return
  End If
  Dim arr() As JsonNode = doc.nodes
  Dim k As Integer = arr(id).kind
  Match k With
    Case JsonNull Then
      JwNull(ByRef w)
    Case JsonBool Then
      JwBool(ByRef w, arr(id).boolVal)
    Case JsonInt Then
      JwLong(ByRef w, arr(id).intVal)
    Case JsonFloat Then
      JwDouble(ByRef w, arr(id).floatVal)
    Case JsonString Then
      JwStringBytes(ByRef w, arr(id).strVal, arr(id).strLen)
    Case JsonArray Then
      JwBeginArr(ByRef w)
      Dim cur As Integer = arr(id).firstChild
      While cur >= 0 Do
        JsonWriteNode(ByRef w, ByRef doc, cur, depth + 1)
        cur = arr(cur).nextSibling
      End While
      JwEndArr(ByRef w)
    Case JsonObject Then
      JwBeginObj(ByRef w)
      Dim cur As Integer = arr(id).firstChild
      While cur >= 0 Do
        If w.depth > 0 Then
          Dim top As Integer = w.depth - 1
          If w.stackCount(top) > 0 Then
            JwPutByte(ByRef w, 44)
          End If
          w.stackCount(top) = w.stackCount(top) + 1
        End If
        JwPutEscapedBytes(ByRef w, arr(cur).key, arr(cur).keyLen)
        JwPutByte(ByRef w, 58)
        w.expectVal = True
        JsonWriteNode(ByRef w, ByRef doc, cur, depth + 1)
        cur = arr(cur).nextSibling
      End While
      JwEndObj(ByRef w)
    Case _ Then
      JwNull(ByRef w)
  End Match
End Sub

Public Function JsonStringify(ByRef a As Arena, ByRef doc As JsonDoc, id As Integer) As Pointer
  Dim need As Integer = JsonMeasureNode(ByRef doc, id, 0) + 16
  Dim w As JsonWriter = JwNew(ByRef a, need)
  If w.overflow Then
    Return 0
  End If
  JsonWriteNode(ByRef w, ByRef doc, id, 0)
  Return JwFinish(ByRef w)
End Function
