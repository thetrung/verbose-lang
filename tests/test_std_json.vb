' Standard Library RFC 8259 JSON parser, DOM, and streaming JsonWriter test suite.

Include "../std/json.vb"

Private Function ApproxEq(a As Double, b As Double, tol As Double) As Boolean
  Dim d As Double = a - b
  If d < 0.0 Then
    d = 0.0 - d
  End If
  Return d <= tol
End Function

Test "Parse primitives: null, bool, 64-bit ints, and floats"
  Dim a As Arena = ArenaCreate(16384)

  Dim dNull As JsonDoc = JsonParse(ByRef a, "  null  ")
  Assert dNull.ok
  Assert JsonIsNull(ByRef dNull, dNull.root)
  Assert JsonKindOf(ByRef dNull, dNull.root) = JsonNull

  Dim dTrue As JsonDoc = JsonParse(ByRef a, "true")
  Assert dTrue.ok
  Assert JsonIsBool(ByRef dTrue, dTrue.root)
  Assert JsonAsBool(ByRef dTrue, dTrue.root, False)

  Dim dFalse As JsonDoc = JsonParse(ByRef a, "false")
  Assert dFalse.ok
  Assert JsonIsBool(ByRef dFalse, dFalse.root)
  Assert Not JsonAsBool(ByRef dFalse, dFalse.root, True)

  Dim dInt As JsonDoc = JsonParse(ByRef a, "-42")
  Assert dInt.ok
  Assert JsonIsInt(ByRef dInt, dInt.root)
  Assert JsonIsNumber(ByRef dInt, dInt.root)
  Assert JsonAsInt(ByRef dInt, dInt.root, 0) = -42
  Assert JsonAsLong(ByRef dInt, dInt.root, 0) = -42
  Assert ApproxEq(JsonAsDouble(ByRef dInt, dInt.root, 0.0), -42.0, 0.0001)

  Dim dNegZero As JsonDoc = JsonParse(ByRef a, "-0")
  Assert dNegZero.ok
  Assert JsonIsInt(ByRef dNegZero, dNegZero.root)
  Assert JsonAsLong(ByRef dNegZero, dNegZero.root, 9) = 0

  Dim max64 As Long = ((Long(1) Shl 62) - Long(1)) + (Long(1) Shl 62)
  Dim dMax64 As JsonDoc = JsonParse(ByRef a, "9223372036854775807")
  Assert dMax64.ok
  Assert JsonIsInt(ByRef dMax64, dMax64.root)
  Assert JsonAsLong(ByRef dMax64, dMax64.root, 0) = max64

  Dim min64 As Long = Long(1) Shl 63
  Dim dMin64 As JsonDoc = JsonParse(ByRef a, "-9223372036854775808")
  Assert dMin64.ok
  Assert JsonIsInt(ByRef dMin64, dMin64.root)
  Assert JsonAsLong(ByRef dMin64, dMin64.root, 0) = min64

  ' 64-bit integer overflow promotes cleanly to JsonFloat
  Dim dBig As JsonDoc = JsonParse(ByRef a, "9223372036854775808")
  Assert dBig.ok
  Assert JsonIsFloat(ByRef dBig, dBig.root)
  Assert JsonAsDouble(ByRef dBig, dBig.root, 0.0) > 9000000000000000000.0

  Dim dF1 As JsonDoc = JsonParse(ByRef a, "3.14159")
  Assert dF1.ok
  Assert JsonIsFloat(ByRef dF1, dF1.root)
  Assert ApproxEq(JsonAsDouble(ByRef dF1, dF1.root, 0.0), 3.14159, 0.00001)

  Dim dF2 As JsonDoc = JsonParse(ByRef a, "-2.5e-3")
  Assert dF2.ok
  Assert JsonIsFloat(ByRef dF2, dF2.root)
  Assert ApproxEq(JsonAsDouble(ByRef dF2, dF2.root, 0.0), -0.0025, 0.000001)

  Dim dF3 As JsonDoc = JsonParse(ByRef a, "1.5E+4")
  Assert dF3.ok
  Assert ApproxEq(JsonAsDouble(ByRef dF3, dF3.root, 0.0), 15000.0, 0.001)

  ArenaDestroy(ByRef a)
End Test

Test "Parse strings, escapes, unicode, and UTF-16 surrogate pairs"
  Dim a As Arena = ArenaCreate(16384)

  Dim dEmpty As JsonDoc = JsonParse(ByRef a, "\"\"")
  Assert dEmpty.ok
  Assert JsonIsString(ByRef dEmpty, dEmpty.root)
  Assert StrEq(JsonAsString(ByRef dEmpty, dEmpty.root, "x"), "")
  Assert JsonStrLen(ByRef dEmpty, dEmpty.root) = 0

  Dim dEsc As JsonDoc = JsonParse(ByRef a, "\"a\\\"b\\\\c\\/d\\ne\\tf\\rg\"")
  Assert dEsc.ok
  Dim sEsc As Pointer = JsonAsString(ByRef dEsc, dEsc.root, "")
  Assert StrEq(sEsc, "a\"b\\c/d\ne\tf\rg")

  ' \u0041 -> 'A', \u00E9 -> UTF-8 0xC3 0xA9, \u20AC -> UTF-8 0xE2 0x82 0xAC
  Dim dUni As JsonDoc = JsonParse(ByRef a, "\"\\u0041\\u00e9\\u20AC\"")
  Assert dUni.ok
  Dim su As Pointer = JsonAsString(ByRef dUni, dUni.root, "")
  Assert JsonStrLen(ByRef dUni, dUni.root) = 6
  Assert (su(0) And 255) = 65
  Assert (su(1) And 255) = 195
  Assert (su(2) And 255) = 169
  Assert (su(3) And 255) = 226
  Assert (su(4) And 255) = 130
  Assert (su(5) And 255) = 172

  ' Surrogate pair \uD83C\uDF31 -> U+1F331 (seedling emoji): F0 9F 8C B1
  Dim dSur As JsonDoc = JsonParse(ByRef a, "\"\\uD83C\\uDF31\"")
  Assert dSur.ok
  Dim ss As Pointer = JsonAsString(ByRef dSur, dSur.root, "")
  Assert JsonStrLen(ByRef dSur, dSur.root) = 4
  Assert (ss(0) And 255) = 240
  Assert (ss(1) And 255) = 159
  Assert (ss(2) And 255) = 140
  Assert (ss(3) And 255) = 177

  ArenaDestroy(ByRef a)
End Test

Test "Parse nested objects, arrays, iteration, and duplicate keys"
  Dim a As Arena = ArenaCreate(32768)

  Dim raw As Pointer = "{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"tools/call\",\"params\":{\"name\":\"agy_ask\",\"args\":[1,true,null,\"hi\",{\"nested\":99}],\"emptyArr\":[],\"emptyObj\":{}}}"
  Dim doc As JsonDoc = JsonParse(ByRef a, raw)
  Assert doc.ok
  Assert JsonIsObject(ByRef doc, doc.root)
  Assert JsonLen(ByRef doc, doc.root) = 4

  Assert StrEq(JsonGetString(ByRef doc, doc.root, "jsonrpc", ""), "2.0")
  Assert JsonGetInt(ByRef doc, doc.root, "id", 0) = 7
  Assert StrEq(JsonGetString(ByRef doc, doc.root, "method", ""), "tools/call")
  Assert JsonHas(ByRef doc, doc.root, "params")
  Assert Not JsonHas(ByRef doc, doc.root, "missing")

  Dim paramsId As Integer = JsonGet(ByRef doc, doc.root, "params")
  Assert JsonIsObject(ByRef doc, paramsId)
  Assert StrEq(JsonGetString(ByRef doc, paramsId, "name", ""), "agy_ask")

  Dim argsId As Integer = JsonGet(ByRef doc, paramsId, "args")
  Assert JsonIsArray(ByRef doc, argsId)
  Assert JsonLen(ByRef doc, argsId) = 5
  Assert JsonAsInt(ByRef doc, JsonAt(ByRef doc, argsId, 0), 0) = 1
  Assert JsonAsBool(ByRef doc, JsonAt(ByRef doc, argsId, 1), False)
  Assert JsonIsNull(ByRef doc, JsonAt(ByRef doc, argsId, 2))
  Assert StrEq(JsonAsString(ByRef doc, JsonAt(ByRef doc, argsId, 3), ""), "hi")
  Dim innerObj As Integer = JsonAt(ByRef doc, argsId, 4)
  Assert JsonGetInt(ByRef doc, innerObj, "nested", 0) = 99
  Assert JsonAt(ByRef doc, argsId, 5) = -1
  Assert JsonAt(ByRef doc, argsId, -1) = -1

  Assert JsonLen(ByRef doc, JsonGet(ByRef doc, paramsId, "emptyArr")) = 0
  Assert JsonLen(ByRef doc, JsonGet(ByRef doc, paramsId, "emptyObj")) = 0

  ' Iterate object keys with JsonFirst / JsonNext / JsonKey
  Dim seenCount As Integer = 0
  Dim cur As Integer = JsonFirst(ByRef doc, doc.root)
  While cur >= 0 Do
    Assert Not IsNull(JsonKey(ByRef doc, cur))
    seenCount = seenCount + 1
    cur = JsonNext(ByRef doc, cur)
  End While
  Assert seenCount = 4

  ' Duplicate key: last value wins in JsonGet
  Dim dDup As JsonDoc = JsonParse(ByRef a, "{\"k\":1,\"k\":2}")
  Assert dDup.ok
  Assert JsonGetInt(ByRef dDup, dDup.root, "k", 0) = 2

  ' Fallback values on type mismatch or missing keys
  Assert JsonGetInt(ByRef doc, doc.root, "method", -99) = -99
  Assert StrEq(JsonGetString(ByRef doc, doc.root, "id", "def"), "def")
  Assert JsonGetBool(ByRef doc, doc.root, "missing", True)

  ArenaDestroy(ByRef a)
End Test

Test "DOM construction, JsonStringify, and round-trip fidelity"
  Dim a As Arena = ArenaCreate(32768)
  Dim doc As JsonDoc = JsonDocNew(ByRef a, 32)
  Assert doc.ok

  Dim root As Integer = JsonNewObject(ByRef doc)
  doc.root = root
  Dim chatId As Long = Long(1002345) * Long(1000000) + Long(678901)
  Assert JsonObjectSet(ByRef a, ByRef doc, root, "ok", JsonNewBool(ByRef doc, True))
  Assert JsonObjectSet(ByRef a, ByRef doc, root, "chat_id", JsonNewInt(ByRef doc, chatId))
  Assert JsonObjectSet(ByRef a, ByRef doc, root, "ratio", JsonNewDouble(ByRef doc, 3.25))
  Assert JsonObjectSet(ByRef a, ByRef doc, root, "nil", JsonNewNull(ByRef doc))
  Assert JsonObjectSet(ByRef a, ByRef doc, root, "msg", JsonNewString(ByRef a, ByRef doc, "hi \"neon\"\nline2\\end"))

  Dim arr As Integer = JsonNewArray(ByRef doc)
  Assert JsonArrayPush(ByRef doc, arr, JsonNewInt(ByRef doc, 10))
  Assert JsonArrayPush(ByRef doc, arr, JsonNewInt(ByRef doc, -20))
  Assert JsonObjectSet(ByRef a, ByRef doc, root, "nums", arr)

  Dim serialized As Pointer = JsonStringify(ByRef a, ByRef doc, root)
  Assert Not IsNull(serialized)

  ' Re-parse serialized output and verify all fields survive round-trip
  Dim rt As JsonDoc = JsonParse(ByRef a, serialized)
  Assert rt.ok
  Assert JsonGetBool(ByRef rt, rt.root, "ok", False)
  Assert JsonGetLong(ByRef rt, rt.root, "chat_id", 0) = chatId
  Assert ApproxEq(JsonGetDouble(ByRef rt, rt.root, "ratio", 0.0), 3.25, 0.0001)
  Assert JsonIsNull(ByRef rt, JsonGet(ByRef rt, rt.root, "nil"))
  Assert StrEq(JsonGetString(ByRef rt, rt.root, "msg", ""), "hi \"neon\"\nline2\\end")
  Dim rtArr As Integer = JsonGet(ByRef rt, rt.root, "nums")
  Assert JsonLen(ByRef rt, rtArr) = 2
  Assert JsonAsInt(ByRef rt, JsonAt(ByRef rt, rtArr, 0), 0) = 10
  Assert JsonAsInt(ByRef rt, JsonAt(ByRef rt, rtArr, 1), 0) = -20

  ArenaDestroy(ByRef a)
End Test

Test "Streaming JsonWriter: auto-commas, escaping, control bytes, and overflow"
  Dim a As Arena = ArenaCreate(16384)

  Dim w As JsonWriter = JwNew(ByRef a, 512)
  JwBeginObj(ByRef w)
  JwFieldString(ByRef w, "jsonrpc", "2.0")
  JwFieldInt(ByRef w, "id", 1)
  JwKey(ByRef w, "result")
  JwBeginObj(ByRef w)
  JwFieldBool(ByRef w, "isError", False)
  JwKey(ByRef w, "items")
  JwBeginArr(ByRef w)
  JwLong(ByRef w, Long(1) Shl 63)
  JwDouble(ByRef w, 0.5)
  JwNull(ByRef w)
  JwString(ByRef w, "a\tb\nc\"d\\e")
  JwRaw(ByRef w, "{\"raw\":true}")
  JwEndArr(ByRef w)
  JwEndObj(ByRef w)
  JwEndObj(ByRef w)

  Assert Not w.overflow
  Dim out As Pointer = JwFinish(ByRef w)
  Assert StrEq(out, "{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"isError\":false,\"items\":[-9223372036854775808,0.5,null,\"a\\tb\\nc\\\"d\\\\e\",{\"raw\":true}]}}")

  ' Verify control character < 0x20 encodes as \u00xx
  Dim ctrl(3) As Byte
  ctrl(0) = 1
  ctrl(1) = 31
  ctrl(2) = 0
  Dim wc As JsonWriter = JwNew(ByRef a, 64)
  JwString(ByRef wc, ctrl)
  Assert Not wc.overflow
  Assert StrEq(JwFinish(ByRef wc), "\"\\u0001\\u001f\"")

  ' Verify NaN and +/-Inf serialize safely as null per RFC 8259
  Dim zero As Double = 0.0
  Dim nanVal As Double = zero / zero
  Dim infVal As Double = 1.0 / zero
  Dim ninfVal As Double = -1.0 / zero
  Dim wn As JsonWriter = JwNew(ByRef a, 32)
  JwDouble(ByRef wn, nanVal)
  Assert StrEq(JwFinish(ByRef wn), "null")
  Dim wi As JsonWriter = JwNew(ByRef a, 32)
  JwDouble(ByRef wi, infVal)
  Assert StrEq(JwFinish(ByRef wi), "null")
  Dim wni As JsonWriter = JwNew(ByRef a, 32)
  JwDouble(ByRef wni, ninfVal)
  Assert StrEq(JwFinish(ByRef wni), "null")

  ' Verify buffer overflow flag triggers cleanly without crashing
  Dim ws As JsonWriter = JwNew(ByRef a, 8)
  JwString(ByRef ws, "this-string-exceeds-eight-bytes")
  Assert ws.overflow
  Assert StrLen(JwFinish(ByRef ws)) = 7

  ArenaDestroy(ByRef a)
End Test

Test "Strict RFC 8259 syntax error rejection and nesting depth guard"
  Dim a As Arena = ArenaCreate(32768)
  Dim np As Pointer = 0

  Dim eNull As JsonDoc = JsonParse(ByRef a, np)
  Assert Not eNull.ok

  Dim eEmpty As JsonDoc = JsonParse(ByRef a, "   ")
  Assert Not eEmpty.ok

  Dim eTrailArr As JsonDoc = JsonParse(ByRef a, "[1,2,]")
  Assert Not eTrailArr.ok

  Dim eTrailObj As JsonDoc = JsonParse(ByRef a, "{\"a\":1,}")
  Assert Not eTrailObj.ok

  Dim eLeadZero As JsonDoc = JsonParse(ByRef a, "01")
  Assert Not eLeadZero.ok

  Dim eNegLeadZero As JsonDoc = JsonParse(ByRef a, "-01")
  Assert Not eNegLeadZero.ok

  Dim ePlus As JsonDoc = JsonParse(ByRef a, "+1")
  Assert Not ePlus.ok

  Dim eDotLead As JsonDoc = JsonParse(ByRef a, ".5")
  Assert Not eDotLead.ok

  Dim eDotTrail As JsonDoc = JsonParse(ByRef a, "1.")
  Assert Not eDotTrail.ok

  Dim eBadExp As JsonDoc = JsonParse(ByRef a, "1e+")
  Assert Not eBadExp.ok

  Dim eSingleQ As JsonDoc = JsonParse(ByRef a, "'hi'")
  Assert Not eSingleQ.ok

  Dim eUnquoted As JsonDoc = JsonParse(ByRef a, "{a:1}")
  Assert Not eUnquoted.ok

  Dim eUnescNl As JsonDoc = JsonParse(ByRef a, "\"line1\nline2\"")
  Assert Not eUnescNl.ok

  Dim eBadEsc As JsonDoc = JsonParse(ByRef a, "\"\\x41\"")
  Assert Not eBadEsc.ok

  Dim eUnterm As JsonDoc = JsonParse(ByRef a, "\"abc")
  Assert Not eUnterm.ok

  Dim eLoneHigh As JsonDoc = JsonParse(ByRef a, "\"\\uD83C\"")
  Assert Not eLoneHigh.ok

  Dim eLoneLow As JsonDoc = JsonParse(ByRef a, "\"\\uDF31\"")
  Assert Not eLoneLow.ok

  Dim eTrailing As JsonDoc = JsonParse(ByRef a, "123 456")
  Assert Not eTrailing.ok

  ' Nesting depth > 64 must fail cleanly without stack overflow
  Dim deep As Pointer = ArenaAllocBytes(ByRef a, 160)
  For i = 0 To 69 Do
    deep(i) = 91
    deep(70 + i) = 93
  End For
  deep(140) = 0
  Dim eDeep As JsonDoc = JsonParse(ByRef a, deep)
  Assert Not eDeep.ok

  ArenaDestroy(ByRef a)
End Test
