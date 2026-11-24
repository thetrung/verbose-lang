' Static globals & Heap Allocator regression suite.

Include "harness.vb"

Test "01_static_global_scalars.vb — Public/Private Static global scalars & linkage"
  Assure RunVb("tests/static_heap/01_static_global_scalars.vb", "--ir") = 0
  Assure OutHas("@pub_counter = global i32 10")
  Assure OutHas("@priv_counter = internal global i32 5")
  Assure OutHas("pub=13 priv=7 label=VB flag=0")
End Test

Test "02_static_global_struct_and_with.vb — Static global struct zero-init, With init, ByRef"
  Assure RunVb("tests/static_heap/02_static_global_struct_and_with.vb", "") = 0
  Assure OutHas("zero=0,0 cfg=8081,3")
End Test

Test "03_static_global_arrays.vb — Static global arrays, literals, Len(), ByRef"
  Assure RunVb("tests/static_heap/03_static_global_arrays.vb", "") = 0
  Assure OutHas("zero=0,99 primes=3,4,6,8 len=4")
End Test

Test "04_static_local_persistent.vb — Function-local Static persistent state across calls"
  Assure RunVb("tests/static_heap/04_static_local_persistent.vb", "") = 0
  Assure OutHas("seq=10,20,30")
End Test

Test "05_static_shadowing.vb — Local Dim shadowing Static global safely"
  Assure RunVb("tests/static_heap/05_static_shadowing.vb", "") = 0
  Assure OutHas("local=8 global=50")
End Test

Test "06_sizeof_types.vb — SizeOf() on primitives & ABI-aligned structs"
  Assure RunVb("tests/static_heap/06_sizeof_types.vb", "") = 0
  Assure OutHas("b=1 s=2 i=4 l=8 f=4 d=8 p=8 st=16")
End Test

Test "07_heap_alloc_primitives_and_realloc.vb — HeapAlloc zero-init, HeapRealloc growth, HeapFree"
  Assure RunVb("tests/static_heap/07_heap_alloc_primitives_and_realloc.vb", "") = 0
  Assure OutHas("zero=0,0")
  Assure OutHas("grown=5,20,25,30")
End Test

Test "08_heap_alloc_structs_and_byref.vb — HeapAlloc struct array, ByRef element, slice pass"
  Assure RunVb("tests/static_heap/08_heap_alloc_structs_and_byref.vb", "") = 0
  Assure OutHas("p1=15,26 sumX=116")
End Test

Test "09_heap_global_slice_and_bytes.vb — Global heap slice Static g_pool() & HeapAllocBytes"
  Assure RunVb("tests/static_heap/09_heap_global_slice_and_bytes.vb", "") = 0
  Assure OutHas("pool=11,44 raw=OK")
End Test

Test "10_static_nonconst_init_err.vb — Reject non-constant Static global initializer"
  Assure RunVb("tests/static_heap/10_static_nonconst_init_err.vb", "") <> 0
  Assure OutHas("compile-time constant")
End Test

Test "11_heap_free_nonptr_err.vb — Reject HeapFree on non-pointer scalar"
  Assure RunVb("tests/static_heap/11_heap_free_nonptr_err.vb", "") <> 0
  Assure OutHas("HeapFree expects a Pointer or heap array")
End Test
