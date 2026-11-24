#!/usr/bin/env python3
"""End-to-end test runner for vbc — the VerBose Bootstrap Compiler written in VerBose-Lang."""

import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
COMPILER = REPO / "verbose"
VBC_SRC = REPO / "bootstrap" / "vbc.vb"

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
        "02_if_else_while.vb",
        "If/Then/Else branching & While loop accumulation",
        """Public Function main() As Integer
  Dim i As Integer = 1
  Dim sum As Integer = 0
  While i <= 10 Do
    If i > 5 Then
      sum = sum + i * 2
    Else
      sum = sum + i
    End If
    i = i + 1
  End While
  printf("sum=%d\\n", sum)
  Return 0
End Function
""",
        ["sum=95"],
    ),
    (
        "03_recursive_fib_and_sub.vb",
        "Recursive Fibonacci function + helper Sub compiled by vbc",
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
]


def main():
    tmp = Path(tempfile.mkdtemp(prefix="vbc_boot_"))
    try:
        vbc_bin = tmp / "vbc"
        # Step 1: Compile bootstrap/vbc.vb using OCaml verbose compiler
        build = subprocess.run(
            [str(COMPILER), str(VBC_SRC), "--no-run", "-o", str(vbc_bin)],
            cwd=str(tmp),
            capture_output=True,
            text=True,
            timeout=20,
        )
        # Clean up emitted .ll beside bootstrap/vbc.vb
        vbc_ll = VBC_SRC.with_suffix(".ll")
        if vbc_ll.exists():
            vbc_ll.unlink()
        if build.returncode != 0:
            print("[FAIL] Build bootstrap/vbc.vb with verbose")
            print(build.stdout)
            print(build.stderr)
            return 1
        print("[PASS] (BUILD) bootstrap/vbc.vb compiled to native binary by verbose")

        passed = 1
        failed = 0
        total = 1 + len(PROGRAMS)

        for fname, desc, source, expected in PROGRAMS:
            src_file = tmp / fname
            ll_file = tmp / (Path(fname).stem + ".ll")
            exe_file = tmp / Path(fname).stem
            src_file.write_text(source)

            # Step 2: Run vbc (VerBose binary!) to compile .vb -> .ll
            r1 = subprocess.run([str(vbc_bin), str(src_file), str(ll_file)], capture_output=True, text=True, timeout=10)
            if r1.returncode != 0:
                failed += 1
                print(f"[FAIL] (VBC) {fname} — {desc}")
                print("    " + (r1.stdout + "\n" + r1.stderr).strip())
                continue

            # Step 3: Compile vbc's emitted LLVM IR with clang -O2
            r2 = subprocess.run(["clang", "-O2", str(ll_file), "-o", str(exe_file)], capture_output=True, text=True, timeout=15)
            if r2.returncode != 0:
                failed += 1
                print(f"[FAIL] (CLANG) {fname} — {desc}")
                print("    " + (r2.stdout + "\n" + r2.stderr).strip())
                continue

            # Step 4: Execute compiled program and verify stdout
            r3 = subprocess.run([str(exe_file)], capture_output=True, text=True, timeout=10)
            out = (r3.stdout + "\n" + r3.stderr).strip()
            missing = [line for line in expected if line not in out]
            if r3.returncode == 0 and not missing:
                passed += 1
                print(f"[PASS] (BOOTSTRAP) {fname} — {desc} [{r1.stdout.strip()}]")
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
