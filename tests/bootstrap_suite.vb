' Self-hosting Stage 1 -> Stage 2 -> Stage 3 fixpoint & feature verification suite (replaces run_bootstrap_tests.py).

Include "harness.vb"

Private Static b_tmpDir(256) As Byte
Private Static b_v2(300) As Byte
Private Static b_ready As Integer = 0

Private Sub EnsureBootstrapDir()
  If b_ready = 1 Then
    Return
  End If
  Dim pid As Integer = getpid()
  snprintf(b_tmpDir, 256, "/tmp/vb_selfhost_%d", pid)
  snprintf(b_v2, 300, "%s/verbose2", b_tmpDir)
  Dim cmd(512) As Byte
  snprintf(cmd, 512, "mkdir -p '%s'", b_tmpDir)
  system(cmd)
  Dim helper(300) As Byte
  snprintf(helper, 300, "%s/helper_mod.vb", b_tmpDir)
  WriteTextFile(helper, "Public Function DoubleIt(x As Integer) As Integer\n  Return x * 2\nEnd Function\n")
  b_ready = 1
End Sub

Private Function RunStage2Prog(fname As Pointer, source As Pointer) As Integer
  Dim srcPath(320) As Byte
  Dim llPath(320) As Byte
  Dim exePath(320) As Byte
  snprintf(srcPath, 320, "%s/%s.vb", b_tmpDir, fname)
  snprintf(llPath, 320, "%s/%s.ll", b_tmpDir, fname)
  snprintf(exePath, 320, "%s/%s", b_tmpDir, fname)
  WriteTextFile(srcPath, source)

  Dim cmd(1536) As Byte
  snprintf(cmd, 1536, "'%s' '%s' '%s' && clang -O2 '%s' -o '%s' && '%s'", b_v2, srcPath, llPath, llPath, exePath, exePath)
  Return RunShell(cmd)
End Function

Test "(STAGE-1 -> STAGE-2 -> STAGE-3 FIXPOINT) self-hosted compiler byte-for-byte fixpoint"
  EnsureBootstrapDir()
  Dim comp(768) As Byte
  GetCompilerAbs(comp, 768)

  Dim v1(300) As Byte
  Dim v2ll(300) As Byte
  Dim v3ll(300) As Byte
  snprintf(v1, 300, "%s/verbose1", b_tmpDir)
  snprintf(v2ll, 300, "%s/verbose2.ll", b_tmpDir)
  snprintf(v3ll, 300, "%s/verbose3.ll", b_tmpDir)

  Dim cmd(2048) As Byte
  ' Stage 1
  snprintf(cmd, 2048, "'%s' src/verbose.vb --no-run -o '%s'", comp, v1)
  Assure RunShell(cmd) = 0, "Stage 1 failed to compile src/verbose.vb"
  unlink("src/verbose.ll")

  ' Stage 2
  snprintf(cmd, 2048, "'%s' src/verbose.vb '%s' && clang -O2 '%s' -o '%s'", v1, v2ll, v2ll, b_v2)
  Assure RunShell(cmd) = 0, "Stage 2 failed to compile src/verbose.vb"

  ' Stage 3 fixpoint
  snprintf(cmd, 2048, "'%s' src/verbose.vb '%s' && diff -q '%s' '%s'", b_v2, v3ll, v2ll, v3ll)
  Assure RunShell(cmd) = 0, "Stage 3 fixpoint diff failed (verbose2.ll != verbose3.ll)"
End Test

Test "(STAGE-2) 01_arith_and_vars — Arithmetic, precedence, unary negation, local variables, Printf"
  EnsureBootstrapDir()
  Assure RunStage2Prog("p01", "Public Function main() As Integer\n  Dim a As Integer = 10\n  Dim b As Integer = 4\n  Dim c As Integer = a + b * 3 - (a / 2)\n  Dim neg As Integer = -c\n  Printf(\"c=%d neg=%d\\n\", c, neg)\n  Return 0\nEnd Function\n") = 0
  Assure OutHas("c=17 neg=-17")
End Test

Test "(STAGE-2) 02_if_else_while_for — If/Then/Else, While, For loops & Boolean And/Or/Not"
  EnsureBootstrapDir()
  Assure RunStage2Prog("p02", "Public Function main() As Integer\n  Dim i As Integer = 1\n  Dim sum As Integer = 0\n  While i <= 10 Do\n    If (i > 5) And (Not (i = 11)) Then\n      sum = sum + i * 2\n    Else\n      sum = sum + i\n    End If\n    i = i + 1\n  End While\n  Dim fsum As Integer = 0\n  For k = 1 To 5\n    fsum = fsum + k\n  End For\n  Printf(\"sum=%d fsum=%d\\n\", sum, fsum)\n  Return 0\nEnd Function\n") = 0
  Assure OutHas("sum=95 fsum=15")
End Test

Test "(STAGE-2) 03_recursive_fib_and_sub — Recursive Fibonacci function + helper Sub"
  EnsureBootstrapDir()
  Assure RunStage2Prog("p03", "Function Fib(n As Integer) As Integer\n  If n <= 1 Then\n    Return n\n  End If\n  Return Fib(n - 1) + Fib(n - 2)\nEnd Function\nSub Report(k As Integer, val As Integer)\n  Printf(\"fib(%d)=%d\\n\", k, val)\nEnd Sub\nPublic Function main() As Integer\n  Dim f10 As Integer = Fib(10)\n  Report(10, f10)\n  Return 0\nEnd Function\n") = 0
  Assure OutHas("fib(10)=55")
End Test

Test "(STAGE-2) 04_structs_enums_byref_arrays — Structure, Enum, With, ByRef, () arrays, Len(), Select Case"
  EnsureBootstrapDir()
  Assure RunStage2Prog("p04", "Enum Mode\n  Add = 1\n  Mul = 2\nEnd Enum\nStructure Pair\n  Dim a As Integer\n  Dim b As Integer\nEnd Structure\nSub Apply(ByRef p As Pair, m As Integer, k As Integer)\n  Select Case m\n    Case Mode.Add\n      p.a = p.a + k\n      p.b = p.b + k\n    Case Mode.Mul\n      p.a = p.a * k\n      p.b = p.b * k\n  End Select\nEnd Sub\nPublic Function main() As Integer\n  Dim arr(4) As Integer = (10, 20, 30, 40)\n  arr(2) = arr(2) + 5\n  Dim p As Pair With ( a = arr(0), b = arr(2) )\n  Apply(ByRef p, Mode.Add, 2)\n  Apply(ByRef p, Mode.Mul, 3)\n  Printf(\"len=%d p=%d,%d\\n\", Len(arr), p.a, p.b)\n  Return 0\nEnd Function\n") = 0
  Assure OutHas("len=4 p=36,111")
End Test

Test "(STAGE-2) 05_arena_and_include — Include module, Static globals, Arena lifecycle & IsNull"
  EnsureBootstrapDir()
  Dim rc As Integer = RunStage2Prog("p05", "Include \"helper_mod.vb\"\nStatic g_total As Integer = 100\nPublic Function main() As Integer\n  Dim a As Arena = ArenaCreate(256)\n  Dim xs() As Integer = ArenaAlloc(ByRef a, Integer, 4)\n  xs(0) = DoubleIt(7)\n  xs(3) = g_total + xs(0)\n  Dim x0 As Integer = xs(0)\n  Dim x3 As Integer = xs(3)\n  Dim used1 As Long = ArenaUsed(ByRef a)\n  ArenaReset(ByRef a)\n  Dim used2 As Long = ArenaUsed(ByRef a)\n  ArenaDestroy(ByRef a)\n  Dim dead As Pointer = ArenaAllocBytes(ByRef a, 8)\n  Printf(\"xs=%d,%d used=%ld,%ld null=%d\\n\", x0, x3, used1, used2, IsNull(dead))\n  Return 0\nEnd Function\n")
  Dim rmCmd(512) As Byte
  snprintf(rmCmd, 512, "rm -rf '%s'", b_tmpDir)
  system(rmCmd)
  Assure rc = 0
  Assure OutHas("xs=14,114 used=16,0 null=1")
End Test
