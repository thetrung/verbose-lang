' Built-in Deterministic Arena Allocator regression suite.

Include "harness.vb"

Test "01_arena_create_alloc_destroy.vb — ArenaCreate, ArenaAlloc, Capacity/Used/Remaining, Destroy"
  Assert RunVb("tests/arena/01_arena_create_alloc_destroy.vb", "") = 0
  Assert OutHas("zero=0,0 cap=256 used=16 rem=240")
  Assert OutHas("vals=10,40")
  Assert OutHas("destroyed cap=0 used=0 null=1")
End Test

Test "02_arena_structs_and_alignment.vb — 8-byte bump alignment, struct array alloc & ByRef mutation"
  Assert RunVb("tests/arena/02_arena_structs_and_alignment.vb", "") = 0
  Assert OutHas("align=8 b0=77 node1=105,2.5 used=56")
End Test

Test "03_arena_reset_and_reuse.vb — ArenaReset O(1) bulk reset + re-zeroing on reuse"
  Assert RunVb("tests/arena/03_arena_reset_and_reuse.vb", "") = 0
  Assert OutHas("reset_used=0 zeroed=0,0")
End Test

Test "04_arena_mark_and_rewind.vb — ArenaMark / ArenaRewind scoped scratch allocations"
  Assert RunVb("tests/arena/04_arena_mark_and_rewind.vb", "") = 0
  Assert OutHas("mark=8 peak=72 rewound=8 perm=7,8 fresh0=0")
End Test

Test "05_arena_from_static_buffer.vb — Zero-malloc ArenaFromBuffer over Static byte array"
  Assert RunVb("tests/arena/05_arena_from_static_buffer.vb", "") = 0
  Assert OutHas("cap=256 used=24 p0=11,22 p2=33,44")
End Test

Test "06_arena_from_stack_buffer.vb — Stack-backed ArenaFromBuffer & ArenaAllocBytes"
  Assert RunVb("tests/arena/06_arena_from_stack_buffer.vb", "") = 0
  Assert OutHas("s=AR used=8 rem=120")
End Test

Test "07_arena_oom_deterministic.vb — Deterministic OOM null return without corrupting offset"
  Assert RunVb("tests/arena/07_arena_oom_deterministic.vb", "") = 0
  Assert OutHas("n1=0 n2=1 used=16 n3=0 final=32")
End Test

Test "08_arena_byref_param_and_global.vb — Global Static Arena passed ByRef into helpers"
  Assert RunVb("tests/arena/08_arena_byref_param_and_global.vb", "") = 0
  Assert OutHas("s1=30 s2=70 used=16")
End Test

Test "09_arena_missing_byref_err.vb — Reject ArenaAlloc without ByRef on arena argument"
  Assert RunVb("tests/arena/09_arena_missing_byref_err.vb", "") <> 0
  Assert OutHas("ArenaAlloc expects ByRef arena")
End Test

Test "10_arena_non_arena_arg_err.vb — Reject ArenaReset on non-Arena variable"
  Assert RunVb("tests/arena/10_arena_non_arena_arg_err.vb", "") <> 0
  Assert OutHas("ArenaReset argument must be an Arena variable")
End Test

Test "11_arena_from_buffer_missing_byref_err.vb — Reject ArenaFromBuffer without ByRef"
  Assert RunVb("tests/arena/11_arena_from_buffer_missing_byref_err.vb", "") <> 0
  Assert OutHas("ArenaFromBuffer expects ByRef buffer")
End Test
