#!/usr/bin/env python3
"""Test runner for VerBose Static globals & Heap Allocator tests."""

import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
COMPILER = REPO / "verbose"
TEST_DIR = Path(__file__).resolve().parent / "static_heap"

# (filename, description, extra_flag, expected_lines, expect_error)
CASES = [
    (
        "01_static_global_scalars.vb",
        "Public/Private Static global scalars & linkage",
        "--ir",
        ["@pub_counter = global i32 10", "@priv_counter = internal global i32 5", "pub=13 priv=7 label=VB flag=0"],
        False,
    ),
    ("02_static_global_struct_and_with.vb", "Static global struct zero-init, With init, ByRef", None, ["zero=0,0 cfg=8081,3"], False),
    ("03_static_global_arrays.vb", "Static global arrays, literals, Len(), ByRef", None, ["zero=0,99 primes=3,4,6,8 len=4"], False),
    ("04_static_local_persistent.vb", "Function-local Static persistent state across calls", None, ["seq=10,20,30"], False),
    ("05_static_shadowing.vb", "Local Dim shadowing Static global safely", None, ["local=8 global=50"], False),
    ("06_sizeof_types.vb", "SizeOf() on primitives & ABI-aligned structs", None, ["b=1 s=2 i=4 l=8 f=4 d=8 p=8 st=16"], False),
    ("07_heap_alloc_primitives_and_realloc.vb", "HeapAlloc zero-init, HeapRealloc growth, HeapFree", None, ["zero=0,0", "grown=5,20,25,30"], False),
    ("08_heap_alloc_structs_and_byref.vb", "HeapAlloc struct array, ByRef element, slice pass", None, ["p1=15,26 sumX=116"], False),
    ("09_heap_global_slice_and_bytes.vb", "Global heap slice Static g_pool() & HeapAllocBytes", None, ["pool=11,44 raw=OK"], False),
    ("10_static_nonconst_init_err.vb", "Reject non-constant Static global initializer", None, ["compile-time constant"], True),
    ("11_heap_free_nonptr_err.vb", "Reject HeapFree on non-pointer scalar", None, ["HeapFree expects a Pointer or heap array"], True),
]

CLANG_STATUS_RE = re.compile(r"Status:\s*clang/exec\s*=\s*(-?\d+)\s+(-?\d+)")


def run_case(vb_path: Path, extra_flag=None):
    tmp = tempfile.mkdtemp(prefix="vbheap_")
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
        return ok, out
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def main():
    passed = 0
    failed = 0
    for filename, desc, extra_flag, expected_lines, expect_error in CASES:
        ok, out = run_case(TEST_DIR / filename, extra_flag=extra_flag)
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
