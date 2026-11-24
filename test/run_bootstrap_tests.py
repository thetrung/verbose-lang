#!/usr/bin/env python3
"""Self-hosting Stage 1 -> Stage 2 -> Stage 3 fixpoint & feature test runner for verbose."""

import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
COMPILER = REPO / "stage0" / "verbose"
SRC_ENTRY = REPO / "src" / "verbose.vb"

PROGRAMS = [
    (
        "01_arith_and_vars.vb",
        "Arithmetic, precedence, unary negation, local variables, printf",
        """Public Function main() As Integer
  Dim a As Integer = 10
  Dim b As Integer = 4
  Dim c As Integer = a + b * 3 - (a / 2)
  Dim neg As Integer = -c
  printf("c=%d neg=%d\\n", c, neg)
  Return 0
End Function
""",
        ["c=17 neg=-17"],
    ),
    (
        "02_if_else_while_for.vb",
        "If/Then/Else, While, For loops & Boolean And/Or/Not",
        """Public Function main() As Integer
  Dim i As Integer = 1
  Dim sum As Integer = 0
  While i <= 10 Do
    If (i > 5) And (Not (i = 11)) Then
      sum = sum + i * 2
    Else
      sum = sum + i
    End If
    i = i + 1
  End While
  Dim fsum As Integer = 0
  For k = 1 To 5
    fsum = fsum + k
  End For
  printf("sum=%d fsum=%d\\n", sum, fsum)
  Return 0
End Function
""",
        ["sum=95 fsum=15"],
    ),
    (
        "03_recursive_fib_and_sub.vb",
        "Recursive Fibonacci function + helper Sub",
        """Function Fib(n As Integer) As Integer
  If n <= 1 Then
    Return n
  End If
  Return Fib(n - 1) + Fib(n - 2)
End Function

Sub Report(k As Integer, val As Integer)
  printf("fib(%d)=%d\\n", k, val)
End Sub

Public Function main() As Integer
  Dim f10 As Integer = Fib(10)
  Report(10, f10)
  Return 0
End Function
""",
        ["fib(10)=55"],
    ),
    (
        "04_structs_enums_byref_arrays.vb",
        "Structure, Enum, With, ByRef, VB () arrays, Len(), Select Case",
        """Enum Mode
  Add = 1
  Mul = 2
End Enum

Structure Pair
  Dim a As Integer
  Dim b As Integer
End Structure

Sub Apply(ByRef p As Pair, m As Integer, k As Integer)
  Select Case m
    Case Mode.Add
      p.a = p.a + k
      p.b = p.b + k
    Case Mode.Mul
      p.a = p.a * k
      p.b = p.b * k
  End Select
End Sub

Public Function main() As Integer
  Dim arr(4) As Integer = {10, 20, 30, 40}
  arr(2) = arr(2) + 5
  Dim p As Pair With { a = arr(0), b = arr(2) }
  Apply(ByRef p, Mode.Add, 2)
  Apply(ByRef p, Mode.Mul, 3)
  printf("len=%d p=%d,%d\\n", Len(arr), p.a, p.b)
  Return 0
End Function
""",
        ["len=4 p=36,111"],
    ),
    (
        "05_arena_and_include.vb",
        "Include module, Static globals, ArenaCreate/Alloc/Used/Reset/Destroy & IsNull",
        """Include "helper_mod.vb"

Static g_total As Integer = 100

Public Function main() As Integer
  Dim a As Arena = ArenaCreate(256)
  Dim xs() As Integer = ArenaAlloc(ByRef a, Integer, 4)
  xs(0) = DoubleIt(7)
  xs(3) = g_total + xs(0)
  Dim x0 As Integer = xs(0)
  Dim x3 As Integer = xs(3)
  Dim used1 As Long = ArenaUsed(ByRef a)
  ArenaReset(ByRef a)
  Dim used2 As Long = ArenaUsed(ByRef a)
  ArenaDestroy(ByRef a)
  Dim dead As Pointer = ArenaAllocBytes(ByRef a, 8)
  printf("xs=%d,%d used=%ld,%ld null=%d\\n", x0, x3, used1, used2, IsNull(dead))
  Return 0
End Function
""",
        ["xs=14,114 used=16,0 null=1"],
    ),
]


def main():
    tmp = Path(tempfile.mkdtemp(prefix="vbc_selfhost_"))
    try:
        (tmp / "helper_mod.vb").write_text(
            "Public Function DoubleIt(x As Integer) As Integer\n  Return x * 2\nEnd Function\n",
            encoding="utf-8",
        )
        verbose1 = tmp / "verbose1"
        verbose2_ll = tmp / "verbose2.ll"
        verbose2 = tmp / "verbose2"
        verbose3_ll = tmp / "verbose3.ll"

        passed = 0
        failed = 0
        total = 3 + len(PROGRAMS)

        # Stage 1: Stage 0 OCaml compiler compiles src/verbose.vb -> verbose1
        s1 = subprocess.run(
            [str(COMPILER), str(SRC_ENTRY), "--no-run", "-o", str(verbose1)],
            cwd=str(tmp),
            capture_output=True,
            text=True,
            timeout=30,
        )
        ll_cleanup = SRC_ENTRY.with_suffix(".ll")
        if ll_cleanup.exists():
            ll_cleanup.unlink()
        if s1.returncode != 0:
            print("[FAIL] (STAGE-1) stage0/verbose failed to compile src/verbose.vb")
            print(s1.stdout)
            print(s1.stderr)
            return 1
        passed += 1
        print("[PASS] (STAGE-1) stage0/verbose compiled src/verbose.vb -> verbose1")

        # Stage 2: verbose1 compiles src/verbose.vb -> verbose2.ll -> clang -O2 -> verbose2
        s2_emit = subprocess.run([str(verbose1), str(SRC_ENTRY), str(verbose2_ll)], capture_output=True, text=True, timeout=30)
        if s2_emit.returncode != 0:
            print("[FAIL] (STAGE-2) verbose1 failed to compile src/verbose.vb")
            print(s2_emit.stdout)
            print(s2_emit.stderr)
            return 1
        s2_cc = subprocess.run(["clang", "-O2", str(verbose2_ll), "-o", str(verbose2)], capture_output=True, text=True, timeout=30)
        if s2_cc.returncode != 0:
            print("[FAIL] (STAGE-2 CLANG) clang failed on verbose2.ll")
            print(s2_cc.stdout)
            print(s2_cc.stderr)
            return 1
        passed += 1
        print(f"[PASS] (STAGE-2) verbose1 self-compiled src/verbose.vb -> verbose2 [{s2_emit.stdout.strip()}]")

        # Stage 3: verbose2 compiles src/verbose.vb -> verbose3.ll and verifies byte-for-byte fixpoint!
        s3_emit = subprocess.run([str(verbose2), str(SRC_ENTRY), str(verbose3_ll)], capture_output=True, text=True, timeout=30)
        if s3_emit.returncode != 0:
            print("[FAIL] (STAGE-3) verbose2 failed to compile src/verbose.vb")
            print(s3_emit.stdout)
            print(s3_emit.stderr)
            return 1
        ir2 = verbose2_ll.read_text(encoding="utf-8")
        ir3 = verbose3_ll.read_text(encoding="utf-8")
        if ir2 != ir3:
            print(f"[FAIL] (STAGE-3 FIXPOINT) verbose2.ll ({len(ir2)}B) != verbose3.ll ({len(ir3)}B)")
            return 1
        passed += 1
        print(f"[PASS] (STAGE-3 FIXPOINT) verbose2.ll == verbose3.ll byte-for-byte identical ({len(ir3)} bytes)!")

        # Feature suite compiled by self-hosted Stage-2 verbose2!
        for fname, desc, source, expected in PROGRAMS:
            src_file = tmp / fname
            ll_file = tmp / (Path(fname).stem + ".ll")
            exe_file = tmp / Path(fname).stem
            src_file.write_text(source, encoding="utf-8")

            r1 = subprocess.run([str(verbose2), str(src_file), str(ll_file)], capture_output=True, text=True, timeout=15)
            if r1.returncode != 0:
                failed += 1
                print(f"[FAIL] (STAGE-2) {fname} — {desc}")
                print("    " + (r1.stdout + "\n" + r1.stderr).strip())
                continue

            r2 = subprocess.run(["clang", "-O2", str(ll_file), "-o", str(exe_file)], capture_output=True, text=True, timeout=15)
            if r2.returncode != 0:
                failed += 1
                print(f"[FAIL] (CLANG) {fname} — {desc}")
                print("    " + (r2.stdout + "\n" + r2.stderr).strip())
                continue

            r3 = subprocess.run([str(exe_file)], capture_output=True, text=True, timeout=10)
            out = (r3.stdout + "\n" + r3.stderr).strip()
            missing = [line for line in expected if line not in out]
            if r3.returncode == 0 and not missing:
                passed += 1
                print(f"[PASS] (STAGE-2) {fname} — {desc}")
            else:
                failed += 1
                print(f"[FAIL] (RUN) {fname} — {desc}")
                print(f"    out={out!r} expected={missing!r}")

        print(f"\nSummary: {passed}/{total} tests passed, {failed} failed.")
        return 0 if failed == 0 else 1
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    sys.exit(main())
