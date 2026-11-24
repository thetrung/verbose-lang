#!/usr/bin/env python3
"""Run the self-hosted VerBose compiler (./verbose) across CLI modes, testcases & all 14 examples."""

import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
COMPILER = REPO / "verbose"

# Each entry: (suite, rel_path, expected_substrings, extra_args, needs_repo_cwd)
EXAMPLES = [
    ("examples", "prototype.vb", ["vec1.z ^ 2 ="], [], False),
    ("examples", "examples/arena.vb", [], [], False),
    ("examples", "examples/arrays.vb", [], [], False),
    ("examples", "examples/boolean.vb", ["logic = 1(True)", "logic = 0(False)"], [], False),
    ("examples", "examples/byref.vb", [], [], False),
    ("examples", "examples/compute.vb", [], [], False),
    ("examples", "examples/enum.vb", [], [], False),
    ("examples", "examples/loop_while.vb", [], [], False),
    ("examples", "examples/nested_struct.vb", [], [], False),
    ("examples", "examples/numberic.vb", [], [], False),
    ("examples", "examples/read_file.vb", [], [], True),
    ("examples", "examples/static_heap.vb", [], [], False),
    ("examples", "examples/structure.vb", ["t.Kind = 4", "t.Value = 4660"], [], False),
    ("examples", "examples/structure2.vb", ["node = 3"], [], False),
]


def run_example(vb_path: Path, expected: list, extra_args: list, needs_repo_cwd: bool):
    tmp = Path(tempfile.mkdtemp(prefix="verbose_ex_"))
    try:
        out_bin = tmp / "out_bin"
        run_cwd = str(REPO) if needs_repo_cwd else str(tmp)
        argv = [str(COMPILER), str(vb_path), "-o", str(out_bin)] + extra_args
        r = subprocess.run(argv, cwd=run_cwd, capture_output=True, text=True, timeout=20)
        ll_file = vb_path.with_suffix(".ll")
        if ll_file.exists():
            ll_file.unlink()
        out = (r.stdout + "\n" + r.stderr).strip()
        if r.returncode != 0:
            lines = out.splitlines()
            return False, lines[-1] if lines else f"rc={r.returncode}"
        missing = [s for s in expected if s not in out]
        if missing:
            return False, f"missing {missing[0]!r}"
        return True, ""
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def run_cli_checks():
    tmp = Path(tempfile.mkdtemp(prefix="verbose_cli_"))
    passed = 0
    failed = 0
    try:
        sample = tmp / "hello.vb"
        sample.write_text(
            'Structure Box\n'
            '  Dim v As Integer\n'
            'End Structure\n'
            'Public Function main() As Integer\n'
            '  Dim b As Box With ( v = 42 )\n'
            '  Printf("hello=%d\\n", b.v)\n'
            '  Return 0\n'
            'End Function\n',
            encoding="utf-8",
        )

        checks = []

        # 1. Direct execution: verbose hello.vb
        r = subprocess.run([str(COMPILER), "hello.vb"], cwd=str(tmp), capture_output=True, text=True, timeout=15)
        checks.append((
            "cli: verbose hello.vb (compile + direct execute)",
            r.returncode == 0 and "hello=42" in r.stdout and (tmp / "hello").exists() and (tmp / "hello.ll").exists(),
            r.stdout + r.stderr,
        ))

        # 2. --ast --emit-only
        r = subprocess.run([str(COMPILER), "hello.vb", "--ast", "--emit-only"], cwd=str(tmp), capture_output=True, text=True, timeout=15)
        checks.append((
            "cli: --ast --emit-only",
            r.returncode == 0 and "Structure Box" in r.stdout and "hello=42" not in r.stdout,
            r.stdout + r.stderr,
        ))

        # 3. --ir --no-run -o custom_bin
        r = subprocess.run([str(COMPILER), "hello.vb", "--ir", "--no-run", "-o", "custom_bin"], cwd=str(tmp), capture_output=True, text=True, timeout=15)
        checks.append((
            "cli: --ir --no-run -o custom_bin",
            r.returncode == 0 and "define i32 @main()" in r.stdout and (tmp / "custom_bin").exists() and "hello=42" not in r.stdout,
            r.stdout + r.stderr,
        ))

        # 4. --emit-only -o custom.ll
        r = subprocess.run([str(COMPILER), "hello.vb", "--emit-only", "-o", "custom.ll"], cwd=str(tmp), capture_output=True, text=True, timeout=15)
        checks.append((
            "cli: --emit-only -o custom.ll",
            r.returncode == 0 and (tmp / "custom.ll").exists() and "define i32 @main()" in (tmp / "custom.ll").read_text(encoding="utf-8"),
            r.stdout + r.stderr,
        ))

        # 5. --ta and --no-ta
        r = subprocess.run([str(COMPILER), "hello.vb", "--ta", "--emit-only"], cwd=str(tmp), capture_output=True, text=True, timeout=15)
        checks.append((
            "cli: --ta verbose pass",
            r.returncode == 0 and "[OK] Type Analyzer" in r.stdout,
            r.stdout + r.stderr,
        ))

        bad = tmp / "bad.vb"
        bad.write_text('Public Function main() As Integer\n  Return "oops"\nEnd Function\n', encoding="utf-8")
        r = subprocess.run([str(COMPILER), "bad.vb", "--emit-only"], cwd=str(tmp), capture_output=True, text=True, timeout=15)
        checks.append((
            "cli: default semantic type check rejects bad code",
            r.returncode != 0 and "Semantic Type Error" in (r.stdout + r.stderr),
            r.stdout + r.stderr,
        ))

        # 6. --help and --version
        r_h = subprocess.run([str(COMPILER), "--help"], cwd=str(tmp), capture_output=True, text=True, timeout=10)
        r_v = subprocess.run([str(COMPILER), "--version"], cwd=str(tmp), capture_output=True, text=True, timeout=10)
        checks.append((
            "cli: --help and --version",
            r_h.returncode == 0 and "--emit-only" in r_h.stdout and r_v.returncode == 0 and "verbose" in r_v.stdout,
            r_h.stdout + r_v.stdout,
        ))

        for name, ok, detail in checks:
            if ok:
                passed += 1
                print(f"[PASS] ({name})")
            else:
                failed += 1
                print(f"[FAIL] ({name}) -> {detail.strip()[:160]}")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)
    return passed, failed


def main():
    if not COMPILER.is_file():
        print(f"Error: {COMPILER} not found. Run 'make build' first.", file=sys.stderr)
        return 1

    total_pass, total_fail = run_cli_checks()

    for suite, rel_path, expected, extra_args, needs_repo_cwd in EXAMPLES:
        vb_path = REPO / rel_path
        ok, detail = run_example(vb_path, expected, extra_args, needs_repo_cwd)
        if ok:
            total_pass += 1
            print(f"[PASS] ({suite}) {vb_path.name}")
        else:
            total_fail += 1
            print(f"[FAIL] ({suite}) {vb_path.name} — {detail}")

    print(f"\nSummary: {total_pass}/{total_pass + total_fail} CLI & example tests passed, {total_fail} failed.")
    return 0 if total_fail == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
