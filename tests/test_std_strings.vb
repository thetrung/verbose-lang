' Standard Library String, StrView, and StrBuilder test suite.

Include "../std/strings.vb"

Test "StrLen, StrCmp, StrEq, and null safety"
  Dim np As Pointer = 0
  Assert StrLen(np) = 0
  Assert StrLen("") = 0
  Assert StrLen("hello") = 5

  Assert StrCmp(np, np) = 0
  Assert StrCmp(np, "") = 0
  Assert StrCmp("", np) = 0
  Assert StrCmp(np, "a") < 0
  Assert StrCmp("a", np) > 0
  Assert StrCmp("abc", "abc") = 0
  Assert StrCmp("abc", "abd") < 0
  Assert StrCmp("abd", "abc") > 0
  Assert StrCmp("ab", "abc") < 0
  Assert StrCmp("abc", "ab") > 0

  ' High-bit unsigned byte comparison (200 > 65)
  Dim hi(2) As Byte
  hi(0) = 200
  hi(1) = 0
  Assert StrCmp(hi, "A") > 0

  Assert StrEq("VerBose", "VerBose")
  Assert Not StrEq("VerBose", "verbose")
  Assert StrEq(np, "")
End Test

Test "StrStartsWith, StrEndsWith, StrIndexOf, StrFind"
  Dim np As Pointer = 0
  Assert StrStartsWith("hello world", "hello")
  Assert StrStartsWith("hello", "")
  Assert StrStartsWith("hello", np)
  Assert Not StrStartsWith("hi", "hello")
  Assert Not StrStartsWith(np, "a")
  Assert StrStartsWith(np, "")

  Assert StrEndsWith("hello.vb", ".vb")
  Assert StrEndsWith("hello.vb", "")
  Assert StrEndsWith("hello.vb", np)
  Assert Not StrEndsWith("vb", ".vb")
  Assert Not StrEndsWith(np, "a")
  Assert StrEndsWith(np, "")

  Assert StrIndexOf(np, 65) = -1
  Assert StrIndexOf("", 65) = -1
  Assert StrIndexOf("banana", 98) = 0
  Assert StrIndexOf("banana", 97) = 1
  Assert StrIndexOf("banana", 122) = -1

  Assert StrFind("hello world", "world") = 6
  Assert StrFind("hello world", "hello") = 0
  Assert StrFind("aaaaa", "aab") = -1
  Assert StrFind("abc", "") = 0
  Assert StrFind("abc", np) = 0
  Assert StrFind(np, "a") = -1
  Assert StrFind("ab", "abcd") = -1
End Test

Test "StrCopy, StrSubstr, StrConcat, StrTrim, StrToUpper, StrToLower"
  Dim a As Arena = ArenaCreate(4096)
  Dim np As Pointer = 0

  Dim c1 As Pointer = StrCopy(ByRef a, "VerBose")
  Assert StrEq(c1, "VerBose")
  Dim cEmpty As Pointer = StrCopy(ByRef a, np)
  Assert StrEq(cEmpty, "")

  Assert StrEq(StrSubstr(ByRef a, "abcdef", 2, 3), "cde")
  Assert StrEq(StrSubstr(ByRef a, "abcdef", 4, 10), "ef")
  Assert StrEq(StrSubstr(ByRef a, "abcdef", -2, 2), "ab")
  Assert StrEq(StrSubstr(ByRef a, "abcdef", 10, 2), "")
  Assert StrEq(StrSubstr(ByRef a, "abcdef", 1, 0), "")
  Assert StrEq(StrSubstr(ByRef a, np, 0, 5), "")

  Assert StrEq(StrConcat(ByRef a, "std-", "strings"), "std-strings")
  Assert StrEq(StrConcat(ByRef a, np, "ok"), "ok")
  Assert StrEq(StrConcat(ByRef a, "ok", np), "ok")
  Assert StrEq(StrConcat(ByRef a, "", ""), "")

  Assert StrEq(StrTrim(ByRef a, "  \t\n hello world \r\n "), "hello world")
  Assert StrEq(StrTrim(ByRef a, "   \t\n "), "")
  Assert StrEq(StrTrim(ByRef a, "clean"), "clean")
  Assert StrEq(StrTrim(ByRef a, np), "")

  Assert StrEq(StrToUpper(ByRef a, "Abc-123!xyz"), "ABC-123!XYZ")
  Assert StrEq(StrToLower(ByRef a, "Abc-123!XYZ"), "abc-123!xyz")
  Assert StrEq(StrToUpper(ByRef a, np), "")
  Assert StrEq(StrToLower(ByRef a, np), "")

  ArenaDestroy(ByRef a)
End Test

Test "StrToInt and IntToStr edge cases"
  Dim a As Arena = ArenaCreate(2048)
  Dim np As Pointer = 0

  Assert StrToInt(np) = 0
  Assert StrToInt("") = 0
  Assert StrToInt("0") = 0
  Assert StrToInt("42") = 42
  Assert StrToInt("  \t -123abc") = -123
  Assert StrToInt("+789") = 789
  Assert StrToInt("-2147483648") = -2147483648
  Assert StrToInt("2147483647") = 2147483647

  Assert StrEq(IntToStr(ByRef a, 0), "0")
  Assert StrEq(IntToStr(ByRef a, 42), "42")
  Assert StrEq(IntToStr(ByRef a, -98765), "-98765")
  Assert StrEq(IntToStr(ByRef a, 2147483647), "2147483647")
  Assert StrEq(IntToStr(ByRef a, -2147483648), "-2147483648")

  ArenaDestroy(ByRef a)
End Test

Test "StrViewFrom and StrViewEq"
  Dim np As Pointer = 0
  Dim v1 As StrView = StrViewFrom("alpha")
  Dim v2 As StrView = StrViewFrom("alpha")
  Dim v3 As StrView = StrViewFrom("beta")
  Dim vSlice As StrView = StrView("alpha-extra", 5)
  Dim vEmpty As StrView = StrViewFrom("")
  Dim vNull As StrView = StrViewFrom(np)

  Assert v1.length = 5
  Assert StrViewEq(v1, v2)
  Assert StrViewEq(v1, vSlice)
  Assert Not StrViewEq(v1, v3)
  Assert StrViewEq(vEmpty, vNull)
  Assert vNull.length = 0
End Test

Test "StrBuilder append, int formatting, capacity truncation, and OOM"
  Dim a As Arena = ArenaCreate(1024)
  Dim sb As StrBuilder = SbNew(ByRef a, 32)
  SbAppend(ByRef sb, "val=")
  SbAppendInt(ByRef sb, -42)
  SbAppendChar(ByRef sb, 44)
  SbAppend(ByRef sb, "ok=")
  SbAppendInt(ByRef sb, 0)
  Dim out As Pointer = SbFinish(ByRef sb)
  Assert StrEq(out, "val=-42,ok=0")
  Assert sb.length = 12

  ' Truncation at capacity (cap=6 holds max 5 chars + NUL)
  Dim sbSmall As StrBuilder = SbNew(ByRef a, 6)
  SbAppend(ByRef sbSmall, "123456789")
  SbAppendChar(ByRef sbSmall, 88)
  SbAppendInt(ByRef sbSmall, 999)
  Dim trunc As Pointer = SbFinish(ByRef sbSmall)
  Assert StrEq(trunc, "12345")
  Assert sbSmall.length = 5

  ' Null-safe SbAppend and tiny cap=0 clamped to 1
  Dim sbTiny As StrBuilder = SbNew(ByRef a, 0)
  Dim np As Pointer = 0
  SbAppend(ByRef sbTiny, np)
  SbAppend(ByRef sbTiny, "abc")
  Assert StrEq(SbFinish(ByRef sbTiny), "")

  ArenaDestroy(ByRef a)

  ' OOM / destroyed arena safety
  Dim sbOom As StrBuilder = SbNew(ByRef a, 16)
  Assert IsNull(sbOom.buf)
  SbAppend(ByRef sbOom, "test")
  Assert IsNull(SbFinish(ByRef sbOom))
End Test
