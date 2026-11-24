' Built-in Test & Assure language feature unit and integration tests.

Include "harness.vb"

Private Structure Pair
  Dim a As Integer
  Dim b As Integer
End Structure

Private Function AddPair(p As Pair) As Integer
  Return p.a + p.b
End Function

Private Sub CheckHelperPositive(x As Integer)
  Assure x > 0, "helper expects positive value"
End Sub

Test "native Assure with booleans, integers, structs, arrays, and arenas"
  Dim p As Pair With ( a = 17, b = 25 )
  Assure AddPair(p) = 42
  Assure AddPair(p) = 42, "17 + 25 must equal 42"
  Assure 1, "non-zero integer is truthy"
  Assure Not (p.a = p.b)

  Dim nums(3) As Integer = (10, 20, 30)
  Assure Len(nums) = 3
  Assure nums(0) + nums(1) + nums(2) = 60

  Dim a As Arena = ArenaCreate(128)
  Dim buf() As Integer = ArenaAlloc(ByRef a, Integer, 4)
  Assure Not IsNull(buf), "arena alloc must succeed"
  buf(3) = 99
  Assure buf(3) = 99
  ArenaDestroy(ByRef a)

  CheckHelperPositive(7)
End Test

Test "failing Assure inside Test reports line, message, continues other tests, and exits 1"
  Dim comp(768) As Byte
  GetCompilerAbs(comp, 768)
  Dim pid As Integer = getpid()
  Dim tmpFile(256) As Byte
  snprintf(tmpFile, 256, "/tmp/vb_assure_fail_%d.vb", pid)

  WriteTextFile(tmpFile, "Test \"will fail\"\n  Assure 1 = 2, \"math broke\"\nEnd Test\n\nTest \"still runs and passes\"\n  Assure 2 + 2 = 4\nEnd Test\n")

  Dim cmd(1024) As Byte
  snprintf(cmd, 1024, "'%s' test '%s'", comp, tmpFile)
  Dim rc As Integer = RunShell(cmd)
  unlink(tmpFile)

  Assure rc = 1, "test suite with failing Assure must exit 1"
  Assure OutHas("[ASSURE FAILED] line 2: math broke"), "must report line and custom message"
  Assure OutHas("[FAIL] will fail"), "must mark failing test"
  Assure OutHas("[PASS] still runs and passes"), "subsequent tests must still execute"
  Assure OutHas("Summary: 1/2 tests passed, 1 failed."), "must print accurate summary"
End Test

Test "Assure in normal main() aborts with exit 1 on failure and passes when true"
  Dim comp(768) As Byte
  GetCompilerAbs(comp, 768)
  Dim pid As Integer = getpid()
  Dim okFile(256) As Byte
  Dim badFile(256) As Byte
  Dim outBin(256) As Byte
  Dim okLl(256) As Byte
  Dim badLl(256) As Byte
  snprintf(okFile, 256, "/tmp/vb_assure_ok_%d.vb", pid)
  snprintf(okLl, 256, "/tmp/vb_assure_ok_%d.ll", pid)
  snprintf(badFile, 256, "/tmp/vb_assure_bad_%d.vb", pid)
  snprintf(badLl, 256, "/tmp/vb_assure_bad_%d.ll", pid)
  snprintf(outBin, 256, "/tmp/vb_assure_bin_%d", pid)

  WriteTextFile(okFile, "Public Function main() As Integer\n  Assure 5 > 3\n  Printf(\"ok_after_assure\\n\")\n  Return 0\nEnd Function\n")
  WriteTextFile(badFile, "Public Function main() As Integer\n  Assure 5 < 3, \"runtime boom\"\n  Printf(\"unreachable\\n\")\n  Return 0\nEnd Function\n")

  Dim cmd(1024) As Byte
  snprintf(cmd, 1024, "'%s' '%s' -o '%s'", comp, okFile, outBin)
  Dim rc1 As Integer = RunShell(cmd)
  Assure rc1 = 0
  Assure OutHas("ok_after_assure")

  snprintf(cmd, 1024, "'%s' '%s' -o '%s'", comp, badFile, outBin)
  Dim rc2 As Integer = RunShell(cmd)
  Assure rc2 = 1, "failed runtime Assure must exit 1"
  Assure OutHas("[ASSURE FAILED] line 2: runtime boom")
  Assure Not OutHas("unreachable"), "code after failed runtime Assure must not execute"

  unlink(okFile)
  unlink(okLl)
  unlink(badFile)
  unlink(badLl)
  unlink(outBin)
End Test

Test "file with both main() and Test blocks runs main normally and Tests in test mode"
  Dim comp(768) As Byte
  GetCompilerAbs(comp, 768)
  Dim pid As Integer = getpid()
  Dim dualFile(256) As Byte
  Dim dualLl(256) As Byte
  Dim dualBin(256) As Byte
  snprintf(dualFile, 256, "/tmp/vb_dual_%d.vb", pid)
  snprintf(dualLl, 256, "/tmp/vb_dual_%d.ll", pid)
  snprintf(dualBin, 256, "/tmp/vb_dual_bin_%d", pid)

  WriteTextFile(dualFile, "Private Function DoubleVal(x As Integer) As Integer\n  Return x * 2\nEnd Function\n\nTest \"double works\"\n  Assure DoubleVal(21) = 42\nEnd Test\n\nPublic Function main() As Integer\n  Printf(\"main_ran=%d\\n\", DoubleVal(5))\n  Return 0\nEnd Function\n")

  Dim cmd(1024) As Byte
  snprintf(cmd, 1024, "'%s' '%s' -o '%s'", comp, dualFile, dualBin)
  Assure RunShell(cmd) = 0
  Assure OutHas("main_ran=10")
  Assure Not OutHas("[PASS] double works")

  snprintf(cmd, 1024, "'%s' test '%s'", comp, dualFile)
  Assure RunShell(cmd) = 0
  Assure OutHas("[PASS] double works")
  Assure Not OutHas("main_ran=10")

  snprintf(cmd, 1024, "'%s' '%s' --ast --emit-only -o '%s'", comp, dualFile, dualLl)
  Assure RunShell(cmd) = 0
  Assure OutHas("Test \"double works\"")
  Assure OutHas("Assure (DoubleVal(21) = 42)")

  unlink(dualFile)
  unlink(dualLl)
  unlink(dualBin)
End Test

Test "Type Analyzer validates Assure condition and message types"
  Dim comp(768) As Byte
  GetCompilerAbs(comp, 768)
  Dim pid As Integer = getpid()
  Dim badCond(256) As Byte
  Dim badMsg(256) As Byte
  snprintf(badCond, 256, "/tmp/vb_ta_acond_%d.vb", pid)
  snprintf(badMsg, 256, "/tmp/vb_ta_amsg_%d.vb", pid)

  WriteTextFile(badCond, "Structure S\n  Dim x As Integer\nEnd Structure\nTest \"bad cond\"\n  Dim s As S\n  Assure s\nEnd Test\n")
  WriteTextFile(badMsg, "Test \"bad msg\"\n  Assure True, 123\nEnd Test\n")

  Dim cmd(1024) As Byte
  snprintf(cmd, 1024, "'%s' test '%s'", comp, badCond)
  Assure RunShell(cmd) <> 0
  Assure OutHas("Semantic Type Error: Assure condition must be Boolean")

  snprintf(cmd, 1024, "'%s' test '%s'", comp, badMsg)
  Assure RunShell(cmd) <> 0
  Assure OutHas("Semantic Type Error: Assure message must be a string Pointer")

  unlink(badCond)
  unlink(badMsg)
End Test
