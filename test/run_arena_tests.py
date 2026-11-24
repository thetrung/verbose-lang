#!/usr/bin/env python3
"""Test runner for VerBose Built-in Arena Allocator tests."""

import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
COMPILER = REPO / "verbose"
TEST_DIR = Path(__file__).resolve().parent / "arena"

CASES = [
    (
        "01_arena_create_alloc_destroy.vb",
        "ArenaCreate, ArenaAlloc, ArenaCapacity/Used/Remaining, ArenaDestroy + post-destroy null",
        ["zero=0,0 cap=256 used=16 rem=240", "vals=10,40", "destroyed cap=0 used=0 null=1"],
        False,
    ),
    (
        "02_arena_structs_and_alignment.vb",
        "8-byte bump alignment, struct array alloc & ByRef element mutation",
        ["align=8 b0=77 node1=105,2.5 used=56"],
        False,
    ),
    (
        "03_arena_reset_and_reuse.vb",
        "ArenaReset O(1) bulk reset + deterministic re-zeroing on reuse",
        ["reset_used=0 zeroed=0,0"],
        False,
    ),
    (
        "04_arena_mark_and_rewind.vb",
        "ArenaMark / ArenaRewind scoped scratch allocations",
        ["mark=8 peak=72 rewound=8 perm=7,8 fresh0=0"],
        False,
    ),
    (
        "05_arena_from_static_buffer.vb",
        "Zero-malloc baremetal-safe ArenaFromBuffer over Static byte array",
        ["cap=256 used=24 p0=11,22 p2=33,44"],
        False,
    ),
    (
        "06_arena_from_stack_buffer.vb",
        "Stack-backed ArenaFromBuffer & ArenaAllocBytes",
        ["s=AR used=8 rem=120"],
        False,
    ),
    (
        "07_arena_oom_deterministic.vb",
        "Deterministic OOM null return without corrupting arena offset",
        ["n1=0 n2=1 used=16 n3=0 final=32"],
        False,
    ),
    (
        "08_arena_byref_param_and_global.vb",
        "Global Static Arena passed ByRef into helper functions",
        ["s1=30 s2=70 used=16"],
        False,
    ),
    (
        "09_arena_missing_byref_err.vb",
        "Reject ArenaAlloc without ByRef on arena argument",
        ["ArenaAlloc expects ByRef arena"],
        True,
    ),
    (
        "10_arena_non_arena_arg_err.vb",
        "Reject ArenaReset on non-Arena variable",
        ["ArenaReset argument must be an Arena variable"],
        True,
    ),
    (
        "11_arena_from_buffer_missing_byref_err.vb",
        "Reject ArenaFromBuffer without ByRef on buffer",
        ["ArenaFromBuffer expects ByRef buffer"],
        True,
    ),
]

CLANG_STATUS_RE = re.compile(r"Status:\s*clang/exec\s*=\s*(-?\d+)\s+(-?\d+)")


def run_case(vb_path: Path):
    tmp = tempfile.mkdtemp(prefix="vbar_")
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
