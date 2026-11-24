#!/usr/bin/env python3
"""Test runner for VerBose C Interop, SysV ABI structs, Raylib demo, escapes & unary minus."""

import os
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
COMPILER = Path(os.environ.get("VERBOSE_COMPILER", str(REPO / "verbose"))).resolve()
TEST_DIR = Path(__file__).resolve().parent / "c_interop"
RAYLIB_DIR = REPO / "examples" / "raylib"

CASES = [
    (
        TEST_DIR / "01_libm_and_unary_neg.vb",
        "libm FFI (-lm), Single/Double coercion, and unary negation",
        ["-lm"],
        ["sqrt=4.0 pow=1024.0 sinf=0.0 cos=1.0 neg=-4.0,-42"],
    ),
    (
        TEST_DIR / "02_large_c_struct_abi.vb",
        "Large >16B C struct byval(%struct) & ByRef pointer mutation",
        [str(TEST_DIR / "big_struct_helper.c")],
        ["s1=60 s2=180 fields=30,60,90"],
    ),
    (
        TEST_DIR / "03_escapes_and_select_return.vb",
        "String escapes (\\\", \\\\, \\t, \\n) & early Return inside Select Case",
        [],
        ['esc="hello"\\t\tpath=C:\\vb', "cls=100,200,-1"],
    ),
    (
        RAYLIB_DIR / "bouncing_ball.vb",
        "Raylib demo (Color/Vector2/Rectangle C ABI + Arena + Include)",
        [str(RAYLIB_DIR / "raylib_headless_shim.c"), "-lm"],
        [
            "InitWindow(800,450,VerBose + Raylib Demo)",
            "SetTargetFPS(60)",
            "Frame 1 ClearBackground(24,24,36,255)",
            "Frame 1 DrawRectangleRec(150.0,410.0,100.0,16.0 rgba=80,200,120,255)",
            "Frame 1 DrawCircleV(780.0,230.0 r=20.0 rgba=230,41,55,255)",
            "Frame 2 DrawCircleV(720.0,260.0 r=20.0 rgba=230,41,55,255)",
            "Frame 3 DrawCircleV(660.0,290.0 r=20.0 rgba=230,41,55,255)",
            "CloseWindow(frames=3)",
        ],
    ),
]

CLANG_STATUS_RE = re.compile(r"Status:\s*clang/exec\s*=\s*(-?\d+)\s+(-?\d+)")


def run_case(vb_path: Path, extra_args):
    tmp = tempfile.mkdtemp(prefix="vbcffi_")
    try:
        out_bin = str(Path(tmp) / "out_bin")
        argv = [str(COMPILER), str(vb_path), "-o", out_bin] + extra_args
        res = subprocess.run(argv, cwd=tmp, capture_output=True, text=True, timeout=15)
        out = (res.stdout + "\n" + res.stderr).strip()
        ok = res.returncode == 0
        m = CLANG_STATUS_RE.search(out)
        if m and (int(m.group(1)) != 0 or int(m.group(2)) != 0):
            ok = False
        ll_file = vb_path.with_suffix(".ll")
        if ll_file.exists():
            ll_file.unlink()
        return ok, out
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def main():
    passed = 0
    failed = 0
    for vb_path, desc, extra_args, expected_lines in CASES:
        ok, out = run_case(vb_path, extra_args)
        missing = [line for line in expected_lines if line not in out]
        case_ok = ok and not missing
        tag = "PASS" if case_ok else "FAIL"
        if case_ok:
            passed += 1
        else:
            failed += 1
        print(f"[{tag}] (RUN) {vb_path.name} — {desc}")
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
