' Array regression suite (fixed arrays, () literals, inferred length, slices, pointer indexing).

Include "harness.vb"

Test "01_array_basic_primitives.vb — Fixed-size zero-init, loop indexing, Len()"
  Assert RunVb("tests/arrays/01_array_basic_primitives.vb", "") = 0
  Assert OutHas("init=0,0 len=5")
  Assert OutHas("vals=10,30,50")
End Test

Test "02_array_literals_and_inferred.vb — Partial literal, single-element, inferred size, copy isolation"
  Assert RunVb("tests/arrays/02_array_literals_and_inferred.vb", "") = 0
  Assert OutHas("a=10,30,0 lenA=5")
  Assert OutHas("b0=1 c0=999 lenB=4")
  Assert OutHas("s1=42 lenS1=1 s2=77 lenS2=1")
End Test

Test "03_array_numeric_types.vb — Long/Single/Double/Byte/Boolean arrays"
  Assert RunVb("tests/arrays/03_array_numeric_types.vb", "") = 0
  Assert OutHas("l=3000000 f=3.25 d=4.75 b=ABC fl=1,0")
End Test

Test "04_array_of_structs.vb — Array of structs with With init & field access"
  Assert RunVb("tests/arrays/04_array_of_structs.vb", "") = 0
  Assert OutHas("p0=10,20 p1=30,40 p2=99,20")
End Test

Test "05_array_inside_struct.vb — Fixed-size array embedded inside Structure"
  Assert RunVb("tests/arrays/05_array_inside_struct.vb", "") = 0
  Assert OutHas("len=4 count=2 vals=11,0,44")
End Test

Test "06_array_byval_vs_byref_fixed.vb — ByVal fixed array isolation vs ByRef mutation"
  Assert RunVb("tests/arrays/06_array_byval_vs_byref_fixed.vb", "") = 0
  Assert OutHas("after_byval=1 after_byref=777")
End Test

Test "07_array_unsized_param.vb — Unsized array params (arr() As T) with ByVal & ByRef"
  Assert RunVb("tests/arrays/07_array_unsized_param.vb", "") = 0
  Assert OutHas("sumA=30 sumB=200")
End Test

Test "08_array_element_byref.vb — ByRef passing of array elements & struct-array elements"
  Assert RunVb("tests/arrays/08_array_element_byref.vb", "") = 0
  Assert OutHas("nums1=12 b2=15")
End Test

Test "09_pointer_byte_indexing.vb — Byte indexing on raw Malloc Pointer & string Pointer"
  Assert RunVb("tests/arrays/09_pointer_byte_indexing.vb", "") = 0
  Assert OutHas("buf=VB msg0=X msg2=Z")
End Test

Test "10_array_zero_size_err.vb — Reject zero-sized array declaration"
  Assert RunVb("tests/arrays/10_array_zero_size_err.vb", "") <> 0
  Assert OutHas("Array size must be greater than 0")
End Test

Test "11_array_lit_overflow_err.vb — Reject array literal exceeding declared size"
  Assert RunVb("tests/arrays/11_array_lit_overflow_err.vb", "") <> 0
  Assert OutHas("exceeding size 2")
End Test

Test "12_array_index_scalar_err.vb — Reject indexing a non-array scalar"
  Assert RunVb("tests/arrays/12_array_index_scalar_err.vb", "") <> 0
  Assert OutHas("Cannot index non-array, non-pointer expression")
End Test
