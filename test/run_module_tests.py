#!/usr/bin/env python3
"""Test runner for VerBose Include module system & Private encapsulation."""

import os
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
COMPILER = Path(os.environ.get("VERBOSE_COMPILER", str(REPO / "verbose"))).resolve()
TEST_DIR = Path(__file__).resolve().parent / "modules"

# (filename, description, extra_args, expected_lines, expect_error)
CASES = [
    ("01_include_basic.vb", "Basic relative Include of struct + struct-returning function", [], ["sum=42 pt=13,24"], False),
    ("02_diamond_and_nested.vb", "Transitive nested & diamond Include deduplication", [], ["a=1 b=3 shared=22"], False),
    ("03_private_isolation.vb", "Module Private encapsulation (same-named private helpers/statics across 3 files)", [], ["alpha=13 beta=42 local=104"], False),
    ("04_include_search_dir.vb", "Include resolution via -I search directory", ["-I", str(TEST_DIR / "common")], ["incdir=25"], False),
    ("05_private_func_denied_err.vb", "Reject cross-module call to Private function", [], ["Cannot access Private function 'helper'"], True),
    ("06_private_global_denied_err.vb", "Reject cross-module read of Private Static global", [], ["Cannot access Private symbol 'secret_factor'"], True),
    ("07_missing_include_err.vb", "Reject missing Included file", [], ["Included file not found"], True),
    ("08_circular_include_err.vb", "Detect and reject circular Include cycle", [], ["Circular Include detected"], True),
]

CLANG_STATUS_RE = re.compile(r"Status:\s*clang/exec\s*=\s*(-?\d+)\s+(-?\d+)")


def run_case(vb_path: Path, extra_args):
    tmp = tempfile.mkdtemp(prefix="vbmod_")
    try:
        out_bin = str(Path(tmp) / "out_bin")
        argv = [str(COMPILER), str(vb_path), "-o", out_bin] + extra_args
        res = subprocess.run(argv, cwd=tmp, capture_output=True, text=True, timeout=15)
        out = (res.stdout + "\n" + res.stderr).strip()
        ok = res.returncode == 0
        m = CLANG_STATUS_RE.search(out)
        if m and (int(m.group(1)) != 0 or int(m.group(2)) != 0):
            ok = False
        # Clean up emitted .ll next to vb_path
        ll_file = vb_path.with_suffix(".ll")
        if ll_file.exists():
            ll_file.unlink()
        return ok, out
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def main():
    passed = 0
    failed = 0
    for filename, desc, extra_args, expected_lines, expect_error in CASES:
        ok, out = run_case(TEST_DIR / filename, extra_args)
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
