#!/usr/bin/env python3
"""Run the self-hosted VerBose compiler (bootstrap/vbc) across ALL positive testcases & examples."""

import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
VBC = REPO / "bootstrap" / "vbc"

# Each entry: (suite, rel_path, expected_substrings, extra_clang_args, needs_repo_cwd)
CASES = [
    # --- syntax (positive) ---
    ("syntax", "test/syntax/01_private_visibility.vb", ["pt=15,20 sum=35"], [], False),
    ("syntax", "test/syntax/02_declare_ffi_stdlib.vb", ["ffi_puts_ok", "abs=42 strlen=7 atoi=1234"], [], False),
    ("syntax", "test/syntax/03_declare_override_builtins.vb", ["first=116 closed=0"], [], True),
    ("syntax", "test/syntax/04_declare_variadic_and_byref.vb", ["matched=2 written=15 text=[a=42 b=99 f=3.5]"], [], False),
    ("syntax", "test/syntax/05_with_initializer_basic.vb", ["v1=3.14,2.75,9999999 v2=1.5,4.5,7"], [], False),
    ("syntax", "test/syntax/06_with_partial_and_nested.vb", ["partial=0.00,7.50 init=10:1.25,2.50", "reassigned=20:0.00,7.50"], [], False),
    ("syntax", "test/syntax/07_inline_comments.vb", ["out=41"], [], False),

    # --- byref (positive) ---
    ("byref", "test/byref/01_byref_struct_whole_assign.vb", ["x=3.50 y=4.25 z=100"], [], False),
    ("byref", "test/byref/02_byref_struct_field_mutate.vb", ["x=9.50 y=12.75 z=777"], [], False),
    ("byref", "test/byref/03_byval_vs_byref_struct.vb", ["after_byval=1,2", "after_default=1,2", "after_byref=555,666"], [], False),
    ("byref", "test/byref/04_byref_struct_function_return.vb", ["r1=15 r2=20 final=20"], [], False),
    ("byref", "test/byref/05_byref_chained_functions.vb", ["stage=3 total=125"], [], False),
    ("byref", "test/byref/06_byref_nested_struct_instance.vb", ["after_box=tag:99,z:40", "after_inner=tag:99,z:888"], [], False),
    ("byref", "test/byref/07_byref_multiple_struct_params.vb", ["p1=20,200 p2=10,100"], [], False),
    ("byref", "test/byref/08_byref_primitive_function.vb", ["a=42 b=11 res=11"], [], False),
    ("byref", "test/byref/09_byref_mixed_params.vb", ["acc=125 delta=25 out=125"], [], False),
    ("byref", "test/byref/10_byref_in_loop_uninit_instance.vb", ["hits=4 score=40"], [], False),
    ("byref", "test/byref/11_byref_nested_field_write.vb", ["z=999"], [], False),
    ("byref", "test/byref/12_byref_var_name_leak.vb", ["vec.x=77 r=12"], [], False),
    ("byref", "test/byref/13_byref_primitive_field_and_forward.vb", ["p.a=16 p.b=2 n=115"], [], False),

    # --- arrays (positive) ---
    ("arrays", "test/arrays/01_array_basic_primitives.vb", ["init=0,0 len=5", "vals=10,30,50"], [], False),
    ("arrays", "test/arrays/02_array_literals_and_inferred.vb", ["a=10,30,0 lenA=5", "b0=1 c0=999 lenB=4"], [], False),
    ("arrays", "test/arrays/03_array_numeric_types.vb", ["l=3000000 f=3.25 d=4.75 b=ABC fl=1,0"], [], False),
    ("arrays", "test/arrays/04_array_of_structs.vb", ["p0=10,20 p1=30,40 p2=99,20"], [], False),
    ("arrays", "test/arrays/05_array_inside_struct.vb", ["len=4 count=2 vals=11,0,44"], [], False),
    ("arrays", "test/arrays/06_array_byval_vs_byref_fixed.vb", ["after_byval=1 after_byref=777"], [], False),
    ("arrays", "test/arrays/07_array_unsized_param.vb", ["sumA=30 sumB=200"], [], False),
    ("arrays", "test/arrays/08_array_element_byref.vb", ["nums1=12 b2=15"], [], False),
    ("arrays", "test/arrays/09_pointer_byte_indexing.vb", ["buf=VB msg0=X msg2=Z"], [], False),

    # --- static_heap (positive) ---
    ("static_heap", "test/static_heap/01_static_global_scalars.vb", ["pub=13 priv=7 label=VB flag=0"], [], False),
    ("static_heap", "test/static_heap/02_static_global_struct_and_with.vb", ["zero=0,0 cfg=8081,3"], [], False),
    ("static_heap", "test/static_heap/03_static_global_arrays.vb", ["zero=0,99 primes=3,4,6,8 len=4"], [], False),
    ("static_heap", "test/static_heap/04_static_local_persistent.vb", ["seq=10,20,30"], [], False),
    ("static_heap", "test/static_heap/05_static_shadowing.vb", ["local=8 global=50"], [], False),
    ("static_heap", "test/static_heap/06_sizeof_types.vb", ["b=1 s=2 i=4 l=8 f=4 d=8 p=8 st=16"], [], False),
    ("static_heap", "test/static_heap/07_heap_alloc_primitives_and_realloc.vb", ["zero=0,0", "grown=5,20,25,30"], [], False),
    ("static_heap", "test/static_heap/08_heap_alloc_structs_and_byref.vb", ["p1=15,26 sumX=116"], [], False),
    ("static_heap", "test/static_heap/09_heap_global_slice_and_bytes.vb", ["pool=11,44 raw=OK"], [], False),

    # --- arena (positive) ---
    ("arena", "test/arena/01_arena_create_alloc_destroy.vb", ["zero=0,0 cap=256 used=16 rem=240", "vals=10,40", "destroyed cap=0 used=0 null=1"], [], False),
    ("arena", "test/arena/02_arena_structs_and_alignment.vb", ["align=8 b0=77 node1=105,2.5 used=56"], [], False),
    ("arena", "test/arena/03_arena_reset_and_reuse.vb", ["reset_used=0 zeroed=0,0"], [], False),
    ("arena", "test/arena/04_arena_mark_and_rewind.vb", ["mark=8 peak=72 rewound=8 perm=7,8 fresh0=0"], [], False),
    ("arena", "test/arena/05_arena_from_static_buffer.vb", ["cap=256 used=24 p0=11,22 p2=33,44"], [], False),
    ("arena", "test/arena/06_arena_from_stack_buffer.vb", ["s=AR used=8 rem=120"], [], False),
    ("arena", "test/arena/07_arena_oom_deterministic.vb", ["n1=0 n2=1 used=16 n3=0 final=32"], [], False),
    ("arena", "test/arena/08_arena_byref_param_and_global.vb", ["s1=30 s2=70 used=16"], [], False),

    # --- type_analyzer (positive) ---
    ("type_analyzer", "test/type_analyzer/01_ta_valid_features.vb", ["ui=0 up=0.000000 p=1.500000,2.500000,9999 gv=7 ok=1 fl=0 b=65"], [], False),

    # --- modules (positive) ---
    ("modules", "test/modules/01_include_basic.vb", ["sum=42 pt=13,24"], [], False),
    ("modules", "test/modules/02_diamond_and_nested.vb", ["a=1 b=3 shared=22"], [], False),
    ("modules", "test/modules/03_private_isolation.vb", ["alpha=13 beta=42 local=104"], [], False),

    # --- c_interop (positive) ---
    ("c_interop", "test/c_interop/01_libm_and_unary_neg.vb", ["sqrt=4.0 pow=1024.0 sinf=0.0 cos=1.0 neg=-4.0,-42"], ["-lm"], False),
    ("c_interop", "test/c_interop/02_large_c_struct_abi.vb", ["s1=60 s2=180 fields=30,60,90"], [str(REPO / "test/c_interop/big_struct_helper.c")], False),
    ("c_interop", "test/c_interop/03_escapes_and_select_return.vb", ['esc="hello"\\t\tpath=C:\\vb', "cls=100,200,-1"], [], False),
    ("c_interop", "examples/raylib/bouncing_ball.vb", ["InitWindow(800,450,VerBose + Raylib Demo)", "Frame 3 DrawCircleV(660.0,290.0 r=20.0 rgba=230,41,55,255)", "CloseWindow(frames=3)"], [str(REPO / "examples/raylib/raylib_headless_shim.c"), "-lm"], False),

    # --- examples (all 14) ---
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


def run_one(vb_path: Path, expected: list, extra_clang: list, needs_repo_cwd: bool):
    tmp = Path(tempfile.mkdtemp(prefix="vbc_compat_"))
    try:
        ll_file = tmp / "out.ll"
        exe_file = tmp / "out_bin"

        # 1. Compile with self-hosted vbc
        r1 = subprocess.run([str(VBC), str(vb_path), str(ll_file)], capture_output=True, text=True, timeout=15)
        if r1.returncode != 0 or not ll_file.exists():
            msg = (r1.stdout + "\n" + r1.stderr).strip().splitlines()
            return False, "vbc", msg[-1] if msg else f"rc={r1.returncode}"

        # 2. Compile emitted LLVM IR with clang -O2
        clang_cmd = ["clang", "-O2", str(ll_file), "-o", str(exe_file)] + extra_clang
        r2 = subprocess.run(clang_cmd, capture_output=True, text=True, timeout=15)
        if r2.returncode != 0:
            err_lines = [l for l in (r2.stderr + "\n" + r2.stdout).splitlines() if "error:" in l]
            return False, "clang", err_lines[0] if err_lines else f"rc={r2.returncode}"

        # 3. Execute compiled native binary
        run_cwd = str(REPO) if needs_repo_cwd else str(tmp)
        r3 = subprocess.run([str(exe_file)], cwd=run_cwd, capture_output=True, text=True, timeout=10)
        out = (r3.stdout + "\n" + r3.stderr).strip()
        if r3.returncode != 0:
            return False, "exec", f"rc={r3.returncode}"
        missing = [s for s in expected if s not in out]
        if missing:
            return False, "output", f"missing {missing[0]!r} in {out[:120]!r}"
        return True, "ok", ""
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def main():
    if not VBC.is_file():
        print(f"Error: {VBC} not found. Run 'make bootstrap' first.", file=sys.stderr)
        return 1

    suite_stats = {}
    total_pass = 0
    total_fail = 0

    for suite, rel_path, expected, extra_clang, needs_repo_cwd in CASES:
        vb_path = REPO / rel_path
        ok, stage, detail = run_one(vb_path, expected, extra_clang, needs_repo_cwd)
        st = suite_stats.setdefault(suite, [0, 0])
        if ok:
            st[0] += 1
            total_pass += 1
            print(f"[PASS] ({suite}) {vb_path.name}")
        else:
            st[1] += 1
            total_fail += 1
            print(f"[FAIL] ({suite}) {vb_path.name} [{stage}] {detail}")

    print("\n--- Per-Suite vbc Compatibility Summary ---")
    for suite, (p, f) in suite_stats.items():
        print(f"  {suite:14s}: {p}/{p + f} passed")
    print(f"\nOverall vbc compatibility: {total_pass}/{total_pass + total_fail} passed, {total_fail} failed.")
    return 0 if total_fail == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
