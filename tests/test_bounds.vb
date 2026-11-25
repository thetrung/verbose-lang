' Array & memory bounds safety tests (v0.7.4):
' 1. Zero-cost compile-time constant OOB errors on fixed-size arrays
' 2. Explicit opt-in --bounds-check runtime checking (default off = zero overhead)
' 3. Explicit bounds-checked std/span.vb views over Heap, Arena, and Static buffers

Include "harness.vb"
Include "std/span.vb"

Test "Compile-time error on constant out-of-bounds fixed array index"
  Dim p(256) As Byte
  snprintf(p, 256, "%s/vb_oob_const_hi_%d.vb", GetTempDir(), getpid())
  Dim srcHi As String = "Public Function main() As Integer\n" &
    "  Dim a(4) As Integer = (1, 2, 3, 4)\n" &
    "  Return a(4)\n" &
    "End Function\n"
  Assert WriteTextFile(p, srcHi)
  Dim rcHi As Integer = RunVb(p, "--emit-only")
  Assert rcHi <> 0, "a(4) on len-4 array must fail at compile time"
  Assert OutHas("out of bounds for fixed array of length 4"), "Error must show array length"

  Dim srcNeg As String = "Public Function main() As Integer\n" &
    "  Dim a(4) As Integer = (1, 2, 3, 4)\n" &
    "  Return a(-1)\n" &
    "End Function\n"
  Assert WriteTextFile(p, srcNeg)
  Dim rcNeg As Integer = RunVb(p, "--emit-only")
  Assert rcNeg <> 0, "a(-1) must fail at compile time"
  Assert OutHas("Array index -1 out of bounds"), "Error must show negative index"

  Dim srcExpr As String = "Public Function main() As Integer\n" &
    "  Dim a(4) As Integer\n" &
    "  a(2 * 3) = 9\n" &
    "  Return 0\n" &
    "End Function\n"
  Assert WriteTextFile(p, srcExpr)
  Dim rcExpr As Integer = RunVb(p, "--emit-only")
  unlink(p)
  Assert rcExpr <> 0, "Constant expression a(2 * 3) on len-4 array must fail at compile time"
End Test

Test "Opt-in --bounds-check catches runtime high and negative OOB cleanly (and default emits zero checks)"
  Dim p(256) As Byte
  snprintf(p, 256, "%s/vb_oob_rt_%d.vb", GetTempDir(), getpid())
  Dim srcOk As String = "Public Function main() As Integer\n" &
    "  Dim a(4) As Integer = (10, 20, 30, 40)\n" &
    "  Dim sum As Integer = 0\n" &
    "  For i = 0 To 3 Do\n" &
    "    sum = sum + a(i)\n" &
    "  End For\n" &
    "  Printf(\"SUM=%d\\n\", sum)\n" &
    "  Return 0\n" &
    "End Function\n"
  Assert WriteTextFile(p, srcOk)
  ' Default build must NOT emit @__vb_bounds_fail
  Dim rcDef As Integer = RunVb(p, "--ir --emit-only")
  Assert rcDef = 0
  Assert Not OutHas("__vb_bounds_fail"), "Default build must have zero runtime bounds check overhead"
  ' --bounds-check build must pass valid loops cleanly
  Dim rcChkOk As Integer = RunVb(p, "--bounds-check")
  Assert rcChkOk = 0, "Valid indexing must pass with --bounds-check"
  Assert OutHas("SUM=100")

  ' High OOB at runtime caught by --bounds-check
  Dim srcHigh As String = "Private Function ReadAt(i As Integer) As Integer\n" &
    "  Dim a(4) As Integer = (10, 20, 30, 40)\n" &
    "  Return a(i)\n" &
    "End Function\n" &
    "Public Function main() As Integer\n" &
    "  Return ReadAt(5)\n" &
    "End Function\n"
  Assert WriteTextFile(p, srcHigh)
  Dim rcHigh As Integer = RunVb(p, "--bounds-check")
  Assert rcHigh <> 0, "Runtime OOB index 5 must abort under --bounds-check"
  Assert OutHas("Runtime Error: Array index 5 out of bounds (4)"), "Error must report exact index & len"

  ' Negative OOB at runtime caught by --bounds-check (via single unsigned ult)
  Dim srcNeg As String = "Private Function ReadAt(i As Integer) As Integer\n" &
    "  Dim a(4) As Integer = (10, 20, 30, 40)\n" &
    "  Return a(i)\n" &
    "End Function\n" &
    "Public Function main() As Integer\n" &
    "  Return ReadAt(-2)\n" &
    "End Function\n"
  Assert WriteTextFile(p, srcNeg)
  Dim rcNeg As Integer = RunVb(p, "--bounds-check")
  Assert rcNeg <> 0, "Runtime negative index -2 must abort under --bounds-check"
  Assert OutHas("Runtime Error: Array index -2 out of bounds (4)"), "Error must report negative index"

  ' Freestanding arm-m with --bounds-check emits llvm.trap (zero libc)
  Dim rcArm As Integer = RunVb(p, "--target arm-m --bounds-check --ir --emit-only")
  unlink(p)
  Assert rcArm = 0, "arm-m --bounds-check must emit valid freestanding IR"
  Assert OutHas("llvm.trap"), "arm-m --bounds-check must lower to llvm.trap"
End Test

Test "std/span.vb: ByteSpan, IntSpan, LongSpan, SingleSpan, PtrSpan checked access & sub-slices"
  Dim a As Arena = ArenaCreate(4096)
  Dim rawInts() As Integer = ArenaAlloc(ByRef a, Integer, 6)
  Dim isp As IntSpan = IntSpanFrom(rawInts, 6)
  For i = 0 To 5 Do
    Assert IntSpanSet(isp, Long(i), (i + 1) * 10)
  End For
  ' Out-of-bounds set/get safely return False without touching memory
  Assert Not IntSpanSet(isp, -1, 999)
  Assert Not IntSpanSet(isp, 6, 999)
  Dim outI As Integer = -1
  Assert IntSpanGet(isp, 3, ByRef outI) And (outI = 40)
  Assert Not IntSpanGet(isp, 6, ByRef outI)
  Assert Not IntSpanGet(isp, -1, ByRef outI)
  Assert IntSpanAt(isp, 5) = 60

  ' Zero-copy checked sub-span
  Dim subI As IntSpan = IntSpanSub(isp, 2, 10)
  Assert subI.length = 4, "Sub-span clamps cleanly to remaining length"
  Assert IntSpanAt(subI, 0) = 30
  Assert IntSpanAt(subI, 3) = 60
  Assert Not IntSpanInBounds(subI, 4)

  ' ByteSpan over HeapAlloc
  Dim rawBytes() As Byte = HeapAlloc(Byte, 8)
  Dim bsp As ByteSpan = ByteSpanFrom(rawBytes, 8)
  Assert ByteSpanSet(bsp, 7, Byte(250))
  Assert Not ByteSpanSet(bsp, 8, Byte(1))
  Dim outB As Byte = Byte(0)
  Assert ByteSpanGet(bsp, 7, ByRef outB) And ((Integer(outB) And 255) = 250)
  HeapFree(rawBytes)

  ' SingleSpan & LongSpan & PtrSpan
  Dim rawF() As Single = ArenaAlloc(ByRef a, Single, 3)
  Dim fsp As SingleSpan = SingleSpanFrom(rawF, 3)
  Assert SingleSpanSet(fsp, 1, 3.5)
  Assert SingleSpanAt(fsp, 1) = 3.5
  Assert Not SingleSpanInBounds(fsp, 3)

  Dim rawL() As Long = ArenaAlloc(ByRef a, Long, 2)
  Dim lsp As LongSpan = LongSpanFrom(rawL, 2)
  Assert LongSpanSet(lsp, 0, Long(100000) * Long(100000))
  Assert LongSpanAt(lsp, 0) > Long(2000000000)
  Assert Not LongSpanInBounds(lsp, 2)

  Dim rawP() As Pointer = ArenaAlloc(ByRef a, Pointer, 2)
  Dim psp As PtrSpan = PtrSpanFrom(rawP, 2)
  Assert PtrSpanSet(psp, 0, "hello")
  Dim outP As Pointer = 0
  Assert PtrSpanGet(psp, 0, ByRef outP) And (Not IsNull(outP))
  Assert Not PtrSpanGet(psp, 2, ByRef outP)

  ' Null / negative length safety
  Dim emptySpan As ByteSpan = ByteSpanFrom(0, 10)
  Assert (emptySpan.length = 0) And (Not ByteSpanInBounds(emptySpan, 0))
  ArenaDestroy(ByRef a)
End Test
