' Language Compression & Ergonomics test suite:
' - Grouped same-type and mixed-type declarations (Dim, Static, Public/Private Static, Structure, Function/Sub/Declare params)
' - Native string concatenation (& and + on Pointer/Byte arrays, compile-time folding, multiline &, runtime concat, TA checks)

Include "harness.vb"

Declare Function strcmp(a, b As Pointer) As Integer

Public Structure Vec3
  Dim x, y, z As Integer
  Dim tag As Byte, active As Boolean
End Structure

Public Static g_a, g_b, g_c As Integer = 5
Private Static g_p = 10, g_q = 20, g_r As Integer
Static g_msgA As Pointer = "Hello, " & "VerBose" & "!"
Static g_msgMulti As Pointer = "Line1\n" &
  "Line2\n" &
  "Line3"

Private Function Sum3(a, b, c As Integer) As Integer
  Return a + b + c
End Function

Private Sub SwapAndAdd(ByRef a, b As Integer, delta As Integer)
  Dim tmp As Integer = a
  a = b + delta
  b = tmp + delta
End Sub

Private Function MixedByRef(ByRef a As Integer, b As Integer) As Integer
  a = a + b
  b = 999
  Return a
End Function

Private Function BumpStatics() As Integer
  Static s1 = 1, s2 = 10 As Integer
  s1 = s1 + 1
  s2 = s2 + 10
  Return s1 + s2
End Function

Test "grouped Dim declarations: same-type, shared init, per-var init, mixed-type, multiline, arrays"
  Dim a, b, c As Integer
  Assert (a = 0) And (b = 0) And (c = 0)

  Dim d, e, f As Integer = 7
  Assert (d = 7) And (e = 7) And (f = 7)

  Dim x = 10, y = 20, z As Integer
  Assert (x = 10) And (y = 20) And (z = 0)

  Dim p = 1, q, r As Integer = 9
  Assert (p = 1) And (q = 9) And (r = 9)

  Dim i1, i2 As Integer, flag As Boolean = True, label As Pointer = "ok"
  Assert (i1 = 0) And (i2 = 0) And flag And (strcmp(label, "ok") = 0)

  Dim m1 = 3,
      m2 = 4,
      m3 = 5 As Integer
  Assert (m1 + m2 + m3) = 12

  Dim arr1(3), arr2(2) As Integer
  Assert (Len(arr1) = 3) And (Len(arr2) = 2)
  arr1(2) = 42
  arr2(1) = 99
  Assert (arr1(2) = 42) And (arr2(1) = 99)

  Dim u1() = (1, 2, 3), u2() = (10, 20) As Integer
  Assert (Len(u1) = 3) And (Len(u2) = 2)
  Assert (u1(0) + u1(1) + u1(2) + u2(0) + u2(1)) = 36
End Test

Test "grouped Public/Private/local Static declarations and Structure fields"
  Assert (g_a = 5) And (g_b = 5) And (g_c = 5)
  Assert (g_p = 10) And (g_q = 20) And (g_r = 0)

  Dim v As Vec3 With ( x = 1, y = 2, z = 3, tag = 7, active = True )
  Assert (v.x = 1) And (v.y = 2) And (v.z = 3) And (v.tag = 7) And v.active

  Assert BumpStatics() = 22
  Assert BumpStatics() = 33
End Test

Test "grouped Function, Sub, and Declare parameters including ByRef propagation"
  Assert Sum3(10, 20, 30) = 60

  Dim a = 3, b = 8 As Integer
  SwapAndAdd(ByRef a, ByRef b, 100)
  Assert (a = 108) And (b = 103)

  Dim m = 5, n = 4 As Integer
  Dim res As Integer = MixedByRef(ByRef m, n)
  Assert (res = 9) And (m = 9) And (n = 4)
End Test

Test "compile-time constant string concatenation (& and +, multiline, escapes, Static globals)"
  Assert strcmp(g_msgA, "Hello, VerBose!") = 0
  Assert strcmp(g_msgMulti, "Line1\nLine2\nLine3") = 0

  Dim s1 As Pointer = "alpha" & "-" & "beta" & "-" & "gamma"
  Assert strcmp(s1, "alpha-beta-gamma") = 0

  Dim s2 As Pointer = "foo" + "_" + "bar"
  Assert strcmp(s2, "foo_bar") = 0

  Dim esc As Pointer = "a\\b" & "n\t\"q\""
  Assert strcmp(esc, "a\\bn\t\"q\"") = 0

  ' Verify compile-time folding emits zero @__vb_str_concat runtime calls in LLVM IR
  Dim comp(768) As Byte
  GetCompilerAbs(comp, 768)
  Dim pid As Integer = getpid()
  Dim tmpVb(256), tmpLl(256) As Byte
  snprintf(tmpVb, 256, "/tmp/vb_fold_%d.vb", pid)
  snprintf(tmpLl, 256, "/tmp/vb_fold_%d.ll", pid)
  WriteTextFile(tmpVb, "Public Function main() As Integer\n  Dim s As Pointer = \"hello \" & \"world\"\n  Printf(\"%s\\n\", s)\n  Return 0\nEnd Function\n")
  Dim cmd(1024) As Byte
  snprintf(cmd, 1024, "'%s' '%s' --emit-only -o '%s'", comp, tmpVb, tmpLl)
  Assert RunShell(cmd) = 0
  Assert FileHas(tmpLl, "hello world")
  Assert Not FileHas(tmpLl, "__vb_str_concat")
  unlink(tmpVb)
  unlink(tmpLl)
End Test

Test "runtime string concatenation (& and +, Byte arrays, null safety, HeapFree)"
  Dim a As Pointer = "Hello, "
  Dim b As Pointer = "Systems"
  Dim c As Pointer = a & b & "!"
  Assert strcmp(c, "Hello, Systems!") = 0
  HeapFree(c)

  Dim d As Pointer = a + b
  Assert strcmp(d, "Hello, Systems") = 0
  HeapFree(d)

  ' Fixed Byte(N) buffer concatenated with Pointer
  Dim buf(16) As Byte
  buf(0) = 86
  buf(1) = 66
  buf(2) = 0
  Dim joined As Pointer = buf & "-Lang"
  Assert strcmp(joined, "VB-Lang") = 0
  HeapFree(joined)

  ' Null pointer safety (treated as empty string)
  Dim np As Pointer = 0
  Dim sLeft As Pointer = np & "right"
  Dim sRight As Pointer = "left" & np
  Assert strcmp(sLeft, "right") = 0
  Assert strcmp(sRight, "left") = 0
  HeapFree(sLeft)
  HeapFree(sRight)
End Test

Test "Type Analyzer rejects invalid '&' concatenation operands"
  Dim comp(768) As Byte
  GetCompilerAbs(comp, 768)
  Dim pid As Integer = getpid()
  Dim bad1(256), bad2(256) As Byte
  snprintf(bad1, 256, "/tmp/vb_concat_bad1_%d.vb", pid)
  snprintf(bad2, 256, "/tmp/vb_concat_bad2_%d.vb", pid)

  WriteTextFile(bad1, "Public Function main() As Integer\n  Dim x As Pointer = \"a\" & 42\n  Return 0\nEnd Function\n")
  WriteTextFile(bad2, "Public Function main() As Integer\n  Dim x As Integer = 1 & 2\n  Return 0\nEnd Function\n")

  Dim cmd(1024) As Byte
  snprintf(cmd, 1024, "'%s' '%s' --emit-only", comp, bad1)
  Assert RunShell(cmd) <> 0
  Assert OutHas("String concatenation '&' requires Pointer or Byte array operands")

  snprintf(cmd, 1024, "'%s' '%s' --emit-only", comp, bad2)
  Assert RunShell(cmd) <> 0
  Assert OutHas("String concatenation '&' requires Pointer or Byte array operands")

  unlink(bad1)
  unlink(bad2)
End Test
