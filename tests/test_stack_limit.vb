' Compile-time stack-frame limit enforcement tests (v0.7.3)
' VerBose never silently spills Dim locals to malloc; oversized stack locals/frames are hard compile errors.

Include "harness.vb"

Test "Reject single oversized Dim local exceeding 64 KB default limit"
  Dim p(256) As Byte
  snprintf(p, 256, "%s/vb_stack_big_var_%d.vb", GetTempDir(), getpid())
  Dim src As String = "Public Function main() As Integer\n" &
    "  Dim big(70000) As Byte\n" &
    "  Return Integer(big(0))\n" &
    "End Function\n"
  Assert WriteTextFile(p, src)
  Dim rc As Integer = RunVb(p, "--emit-only")
  unlink(p)
  Assert rc <> 0, "Single 70 KB Dim local must fail compilation"
  Assert OutHas("exceeding max stack limit"), "Error must explain stack limit"
  Assert OutHas("HeapAlloc, ArenaAlloc, or Static"), "Error must suggest explicit allocators"
End Test

Test "Reject cumulative multi-variable function stack frame exceeding 64 KB"
  Dim p(256) As Byte
  snprintf(p, 256, "%s/vb_stack_cumul_%d.vb", GetTempDir(), getpid())
  Dim src As String = "Public Function main() As Integer\n" &
    "  Dim a(40000) As Byte\n" &
    "  Dim b(40000) As Byte\n" &
    "  Return Integer(a(0)) + Integer(b(0))\n" &
    "End Function\n"
  Assert WriteTextFile(p, src)
  Dim rc As Integer = RunVb(p, "--emit-only")
  unlink(p)
  Assert rc <> 0, "Cumulative 80 KB stack frame must fail compilation"
  Assert OutHas("stack frame requires"), "Error must report cumulative frame size"
End Test

Test "Reject oversized Structure local on stack"
  Dim p(256) As Byte
  snprintf(p, 256, "%s/vb_stack_struct_%d.vb", GetTempDir(), getpid())
  Dim src As String = "Structure HugeBlock\n" &
    "  Dim data(20000) As Integer\n" &
    "End Structure\n" &
    "Public Function main() As Integer\n" &
    "  Dim blk As HugeBlock\n" &
    "  Return blk.data(0)\n" &
    "End Function\n"
  Assert WriteTextFile(p, src)
  Dim rc As Integer = RunVb(p, "--emit-only")
  unlink(p)
  Assert rc <> 0, "80 KB Structure local must fail compilation"
  Assert OutHas("exceeding max stack limit"), "Error must catch large struct local"
End Test

Test "Allow large Static, HeapAlloc, and ArenaAlloc buffers (zero stack overflow)"
  Static sBig(100000) As Byte
  sBig(99999) = Byte(42)
  Dim hBig() As Byte = HeapAlloc(Byte, 100000)
  hBig(99999) = Byte(7)
  Dim a As Arena = ArenaCreate(131072)
  Dim aBig() As Byte = ArenaAlloc(ByRef a, Byte, 100000)
  aBig(99999) = Byte(9)
  Assert (Integer(sBig(99999)) + Integer(hBig(99999)) + Integer(aBig(99999))) = 58
  HeapFree(hBig)
  ArenaDestroy(ByRef a)
End Test

Test "Target-aware arm-m 4 KB limit and explicit --max-stack CLI override"
  Dim p(256) As Byte
  snprintf(p, 256, "%s/vb_stack_arm_%d.vb", GetTempDir(), getpid())
  Dim src As String = "Public Function main() As Integer\n" &
    "  Dim buf(5000) As Byte\n" &
    "  Return Integer(buf(0))\n" &
    "End Function\n"
  Assert WriteTextFile(p, src)
  ' 5 KB passes on 64-bit host (64 KB default)
  Dim rcHost As Integer = RunVb(p, "--emit-only")
  Assert rcHost = 0, "5 KB local must pass on hosted 64-bit target"
  ' 5 KB fails on arm-m (4 KB default)
  Dim rcArm As Integer = RunVb(p, "--target arm-m --emit-only")
  Assert rcArm <> 0, "5 KB local must fail on arm-m 4 KB stack limit"
  Assert OutHas("4096 bytes"), "arm-m error must show 4096 byte limit"
  ' Explicit --max-stack override allows 5 KB on arm-m
  Dim rcOv As Integer = RunVb(p, "--target arm-m --max-stack 8192 --emit-only")
  Assert rcOv = 0, "Explicit --max-stack 8192 must allow 5 KB on arm-m"
  ' Invalid --max-stack rejected
  Dim rcBad As Integer = RunVb(p, "--max-stack -1 --emit-only")
  unlink(p)
  Assert rcBad <> 0, "Invalid --max-stack must fail"
End Test
