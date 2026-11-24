#!/usr/bin/env python3
"""Test runner for VerBose ByRef test cases (struct, instance, functions)."""

import os
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
COMPILER = REPO / "verbose"
TEST_DIR = Path(__file__).resolve().parent / "byref"

CASES = [
    ("01_byref_struct_whole_assign.vb", "ByRef Sub whole-struct reassignment", ["x=3.50 y=4.25 z=100"]),
    ("02_byref_struct_field_mutate.vb", "ByRef Sub field-level mutation", ["x=9.50 y=12.75 z=777"]),
    (
        "03_byval_vs_byref_struct.vb",
        "ByVal vs default vs ByRef struct isolation",
        ["after_byval=1,2", "after_default=1,2", "after_byref=555,666"],
    ),
    ("04_byref_struct_function_return.vb", "ByRef Function mutating struct + return", ["r1=15 r2=20 final=20"]),
    ("05_byref_chained_functions.vb", "Chained ByRef forwarding (Sub->Sub->Func)", ["stage=3 total=125"]),
    (
        "06_byref_nested_struct_instance.vb",
        "ByRef on outer nested struct & inner field",
        ["after_box=tag:99,z:40", "after_inner=tag:99,z:888"],
    ),
    ("07_byref_multiple_struct_params.vb", "Multiple ByRef struct params (swap)", ["p1=20,200 p2=10,100"]),
    ("08_byref_primitive_function.vb", "ByRef on primitive params (Sub & Function)", ["a=42 b=11 res=11"]),
    ("09_byref_mixed_params.vb", "Mixed ByRef struct + ByVal primitive", ["acc=125 delta=25 out=125"]),
    ("10_byref_in_loop_uninit_instance.vb", "Uninit struct instance mutated ByRef in For", ["hits=4 score=40"]),
    ("11_byref_nested_field_write.vb", "Deep nested field write (b.vec.z = ...) via ByRef", ["z=999"]),
    ("12_byref_var_name_leak.vb", "Cross-function var_types leak with reused param name", ["vec.x=77 r=12"]),
    ("13_byref_primitive_field_and_forward.vb", "ByRef primitive struct field & forwarded primitive", ["p.a=16 p.b=2 n=115"]),
]

CLANG_STATUS_RE = re.compile(r"Status:\s*clang/exec\s*=\s*(-?\d+)\s+(-?\d+)")


def run_case(vb_path: Path, extra_flag=None):
    tmp = tempfile.mkdtemp(prefix="vbtest_")
    try:
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
    for filename, desc, expected_lines in CASES:
        vb_path = TEST_DIR / filename
        ok, rc, out = run_case(vb_path)
        missing = [line for line in expected_lines if line not in out]
        case_ok = ok and not missing

        ta_ok, _, ta_out = run_case(vb_path, "--ta")
        ta_err = ""
        if not ta_ok:
            for line in ta_out.splitlines():
                if "Error" in line or "error" in line:
                    ta_err = line.strip()
                    break

        tag = "PASS" if case_ok else "FAIL"
        ta_tag = "TA:OK" if ta_ok else f"TA:FAIL ({ta_err or 'err'})"
        if case_ok:
            passed += 1
        else:
            failed += 1
        print(f"[{tag}] {filename} — {desc} | {ta_tag}")
        if not case_ok:
            # Print compact diagnostic
            diag_lines = [l for l in out.splitlines() if l.strip()]
            for l in diag_lines:
                if any(k in l for k in ("error:", "Error", "Status:", "x=", "a=", "r1=", "stage=", "after_", "p1=", "acc=", "hits=")):
                    print("    " + l)
            if missing:
                print(f"    expected: {missing}")

    print(f"\nSummary: {passed}/{len(CASES)} runtime tests passed, {failed} failed.")
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
