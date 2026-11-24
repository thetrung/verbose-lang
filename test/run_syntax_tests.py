#!/usr/bin/env python3
"""Test runner for VerBose Lexer/Parser Gaps: Private, Declare FFI, With initializers, inline comments."""

import os
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
COMPILER = REPO / "verbose"
TEST_DIR = Path(__file__).resolve().parent / "syntax"

# (filename, description, extra_flag, expected_lines, expect_error)
CASES = [
    (
        "01_private_visibility.vb",
        "Private Structure/Enum/Function/Sub with internal linkage",
        "--ir",
        [
            "define internal i32 @AddXY",
            "define internal void @BumpX",
            "define  i32 @main()",
            "pt=15,20 sum=35",
        ],
        False,
    ),
    (
        "02_declare_ffi_stdlib.vb",
        "Declare FFI calls to C stdlib (abs, strlen, atoi, puts)",
        None,
        ["ffi_puts_ok", "abs=42 strlen=7 atoi=1234"],
        False,
    ),
    (
        "03_declare_override_builtins.vb",
        "Declare deduplication with built-in C functions + fclose",
        None,
        ["first=116 closed=0"],
        False,
    ),
    (
        "04_declare_variadic_and_byref.vb",
        "Declare variadic FFI (sscanf with ByRef + snprintf float promotion)",
        None,
        ["matched=2 written=15 text=[a=42 b=99 f=3.5]"],
        False,
    ),
    (
        "05_with_initializer_basic.vb",
        "With { ... } multi-line, single-line, out-of-order, trailing comma, Single literal",
        None,
        ["v1=3.14,2.75,9999999 v2=1.5,4.5,7"],
        False,
    ),
    (
        "06_with_partial_and_nested.vb",
        "With partial zero-init, nested With, and expression reassignment",
        None,
        ["partial=0.00,7.50 init=10:1.25,2.50", "reassigned=20:0.00,7.50"],
        False,
    ),
    (
        "07_inline_comments.vb",
        "Inline comments and EOF comment without trailing newline",
        None,
        ["out=41"],
        False,
    ),
    (
        "08_with_unknown_field_err.vb",
        "Reject unknown field name in With initializer",
        None,
        ["Structure 'Point' has no field named 'nonexistent'"],
        True,
    ),
    (
        "09_with_duplicate_field_err.vb",
        "Reject duplicate field name in With initializer",
        None,
        ["Duplicate field 'x' in 'Point' With initializer"],
        True,
    ),
    (
        "10_declare_byref_check_err.vb",
        "Enforce ByRef at call site for Declare externs",
        None,
        ["expects ByRef at call site"],
        True,
    ),
]

CLANG_STATUS_RE = re.compile(r"Status:\s*clang/exec\s*=\s*(-?\d+)\s+(-?\d+)")


def run_case(vb_path: Path, extra_flag=None):
    tmp = tempfile.mkdtemp(prefix="vbsyn_")
    try:
        lib_dir = REPO / "lib"
        if lib_dir.is_dir():
            os.symlink(lib_dir, Path(tmp) / "lib")
        shutil.copyfile(vb_path, Path(tmp) / "target.vb")
        argv = [str(COMPILER), "target.vb"]
        if extra_flag:
            argv.append(extra_flag)
        res = subprocess.run(argv, cwd=tmp, capture_output=True, text=True, timeout=15)
        out = (res.stdout + "\n" + res.stderr).strip()
        ok = res.returncode == 0
        m = CLANG_STATUS_RE.search(out)
        if m and (int(m.group(1)) != 0 or int(m.group(2)) != 0):
            ok = False
        return ok, res.returncode, out
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def main():
    passed = 0
    failed = 0
    for filename, desc, extra_flag, expected_lines, expect_error in CASES:
        vb_path = TEST_DIR / filename
        ok, rc, out = run_case(vb_path, extra_flag=extra_flag)
        missing = [line for line in expected_lines if line not in out]
        if expect_error:
            case_ok = (not ok) and not missing
        else:
            case_ok = ok and not missing

        tag = "PASS" if case_ok else "FAIL"
        kind = "ERR-CHECK" if expect_error else "RUN"
        if case_ok:
            passed += 1
        else:
            failed += 1
        print(f"[{tag}] ({kind}) {filename} — {desc}")
        if not case_ok:
            for line in out.splitlines():
                if line.strip():
                    print("    " + line)
            if missing:
                print(f"    expected: {missing}")

    print(f"\nSummary: {passed}/{len(CASES)} tests passed, {failed} failed.")
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
