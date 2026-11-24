#!/usr/bin/env python3
"""Test runner for VerBose Type Analyzer (--ta) semantic checks."""

import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
COMPILER = REPO / "stage0" / "verbose"
TEST_DIR = Path(__file__).resolve().parent / "type_analyzer"

CASES = [
    (
        "01_ta_valid_features.vb",
        "Uninit Dim, struct ctor, literal widening, Boolean ops, shadowing with --ta",
        ["[OK] Type Analyzer", "ui=0 up=0.000000 p=1.500000,2.500000,9999 gv=7 ok=1 fl=0 b=65"],
        False,
    ),
    ("02_ta_return_mismatch_err.vb", "Reject return type mismatch", ["Semantic Type Error", "Return type mismatch"], True),
    ("03_ta_sub_returns_val_err.vb", "Reject Sub returning a value", ["Semantic Type Error", "Sub cannot return a value"], True),
    ("04_ta_func_bare_return_err.vb", "Reject bare Return in non-void Function", ["Semantic Type Error", "Function must return a value"], True),
    ("05_ta_binop_mismatch_err.vb", "Reject binary op type mismatch", ["Semantic Type Error", "Type mismatch in binary operation"], True),
    ("06_ta_float_bitwise_err.vb", "Reject bitwise op on floating point", ["Semantic Type Error", "Unsupported bitwise operation on floating point"], True),
    ("07_ta_arity_mismatch_err.vb", "Reject wrong argument count", ["Semantic Type Error", "expects 2 argument(s), got 1"], True),
    ("08_ta_arg_type_mismatch_err.vb", "Reject wrong argument type", ["Semantic Type Error", "type mismatch: expected Vec, got Boolean"], True),
    ("09_ta_undef_func_err.vb", "Reject undefined function call", ["Semantic Type Error", "Undefined function or constructor: NoSuchFunc"], True),
    ("10_ta_duplicate_dim_err.vb", "Reject duplicate local Dim in same scope", ["Semantic Type Error", "is already declared"], True),
    ("11_ta_nonbool_if_cond_err.vb", "Reject struct value as If condition", ["Semantic Type Error", "If condition must be Boolean"], True),
    ("12_ta_with_field_type_err.vb", "Reject mismatched field type in With initializer", ["Semantic Type Error", "Type mismatch for field 'a'"], True),
]

CLANG_STATUS_RE = re.compile(r"Status:\s*clang/exec\s*=\s*(-?\d+)\s+(-?\d+)")


def run_case(vb_path: Path):
    tmp = tempfile.mkdtemp(prefix="vbta_")
    try:
        shutil.copyfile(vb_path, Path(tmp) / "target.vb")
        res = subprocess.run([str(COMPILER), "target.vb", "--ta"], cwd=tmp, capture_output=True, text=True, timeout=15)
        out = (res.stdout + "\n" + res.stderr).strip()
        ok = res.returncode == 0
        m = CLANG_STATUS_RE.search(out)
        if m and (int(m.group(1)) != 0 or int(m.group(2)) != 0):
            ok = False
        return ok, out
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def main():
    passed = 0
    failed = 0
    for filename, desc, expected_lines, expect_error in CASES:
        ok, out = run_case(TEST_DIR / filename)
        missing = [line for line in expected_lines if line not in out]
        case_ok = ((not ok) and not missing) if expect_error else (ok and not missing)
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
