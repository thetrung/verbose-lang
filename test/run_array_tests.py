#!/usr/bin/env python3
"""Test runner for VerBose Array tests."""

import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
COMPILER = REPO / "verbose"
TEST_DIR = Path(__file__).resolve().parent / "arrays"

CASES = [
    ("01_array_basic_primitives.vb", "Fixed-size zero-init, loop indexing, Len()", ["init=0,0 len=5", "vals=10,30,50"], False),
    ("02_array_literals_and_inferred.vb", "Partial literal, inferred size, value copy isolation", ["a=10,30,0 lenA=5", "b0=1 c0=999 lenB=4"], False),
    ("03_array_numeric_types.vb", "Long/Single/Double/Byte/Boolean arrays", ["l=3000000 f=3.25 d=4.75 b=ABC fl=1,0"], False),
    ("04_array_of_structs.vb", "Array of structs with With init & field access", ["p0=10,20 p1=30,40 p2=99,20"], False),
    ("05_array_inside_struct.vb", "Fixed-size array embedded inside Structure", ["len=4 count=2 vals=11,0,44"], False),
    ("06_array_byval_vs_byref_fixed.vb", "ByVal fixed array isolation vs ByRef mutation", ["after_byval=1 after_byref=777"], False),
    ("07_array_unsized_param.vb", "Unsized array params (arr() As T) with ByVal & ByRef", ["sumA=30 sumB=200"], False),
    ("08_array_element_byref.vb", "ByRef passing of array elements & struct-array elements", ["nums1=12 b2=15"], False),
    ("09_pointer_byte_indexing.vb", "Byte indexing on raw malloc Pointer & string Pointer", ["buf=VB msg0=X msg2=Z"], False),
    ("10_array_zero_size_err.vb", "Reject zero-sized array declaration", ["Array size must be greater than 0"], True),
    ("11_array_lit_overflow_err.vb", "Reject array literal exceeding declared size", ["exceeding size 2"], True),
    ("12_array_index_scalar_err.vb", "Reject indexing a non-array scalar", ["Cannot index non-array, non-pointer expression"], True),
]

CLANG_STATUS_RE = re.compile(r"Status:\s*clang/exec\s*=\s*(-?\d+)\s+(-?\d+)")


def run_case(vb_path: Path):
    tmp = tempfile.mkdtemp(prefix="vbarr_")
    try:
        shutil.copyfile(vb_path, Path(tmp) / "target.vb")
        res = subprocess.run([str(COMPILER), "target.vb"], cwd=tmp, capture_output=True, text=True, timeout=15)
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
