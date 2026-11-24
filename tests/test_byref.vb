' ByRef regression suite (struct, instance, functions, call-site enforcement).

Include "harness.vb"

Test "01_byref_struct_whole_assign.vb — ByRef Sub whole-struct reassignment"
  Assert RunVb("tests/byref/01_byref_struct_whole_assign.vb", "") = 0
  Assert OutHas("x=3.50 y=4.25 z=100")
End Test

Test "02_byref_struct_field_mutate.vb — ByRef Sub field-level mutation"
  Assert RunVb("tests/byref/02_byref_struct_field_mutate.vb", "") = 0
  Assert OutHas("x=9.50 y=12.75 z=777")
End Test

Test "03_byval_vs_byref_struct.vb — ByVal vs default vs ByRef struct isolation"
  Assert RunVb("tests/byref/03_byval_vs_byref_struct.vb", "") = 0
  Assert OutHas("after_byval=1,2")
  Assert OutHas("after_default=1,2")
  Assert OutHas("after_byref=555,666")
End Test

Test "04_byref_struct_function_return.vb — ByRef Function mutating struct + return"
  Assert RunVb("tests/byref/04_byref_struct_function_return.vb", "") = 0
  Assert OutHas("r1=15 r2=20 final=20")
End Test

Test "05_byref_chained_functions.vb — Chained ByRef forwarding (Sub->Sub->Func)"
  Assert RunVb("tests/byref/05_byref_chained_functions.vb", "") = 0
  Assert OutHas("stage=3 total=125")
End Test

Test "06_byref_nested_struct_instance.vb — ByRef on outer nested struct & inner field"
  Assert RunVb("tests/byref/06_byref_nested_struct_instance.vb", "") = 0
  Assert OutHas("after_box=tag:99,z:40")
  Assert OutHas("after_inner=tag:99,z:888")
End Test

Test "07_byref_multiple_struct_params.vb — Multiple ByRef struct params (swap)"
  Assert RunVb("tests/byref/07_byref_multiple_struct_params.vb", "") = 0
  Assert OutHas("p1=20,200 p2=10,100")
End Test

Test "08_byref_primitive_function.vb — ByRef on primitive params (Sub & Function)"
  Assert RunVb("tests/byref/08_byref_primitive_function.vb", "") = 0
  Assert OutHas("a=42 b=11 res=11")
End Test

Test "09_byref_mixed_params.vb — Mixed ByRef struct + ByVal primitive"
  Assert RunVb("tests/byref/09_byref_mixed_params.vb", "") = 0
  Assert OutHas("acc=125 delta=25 out=125")
End Test

Test "10_byref_in_loop_uninit_instance.vb — Uninit struct instance mutated ByRef in For"
  Assert RunVb("tests/byref/10_byref_in_loop_uninit_instance.vb", "") = 0
  Assert OutHas("hits=4 score=40")
End Test

Test "11_byref_nested_field_write.vb — Deep nested field write (b.vec.z = ...) via ByRef"
  Assert RunVb("tests/byref/11_byref_nested_field_write.vb", "") = 0
  Assert OutHas("z=999")
End Test

Test "12_byref_var_name_leak.vb — Cross-function var_types leak with reused param name"
  Assert RunVb("tests/byref/12_byref_var_name_leak.vb", "") = 0
  Assert OutHas("vec.x=77 r=12")
End Test

Test "13_byref_primitive_field_and_forward.vb — ByRef primitive struct field & forwarded primitive"
  Assert RunVb("tests/byref/13_byref_primitive_field_and_forward.vb", "") = 0
  Assert OutHas("p.a=16 p.b=2 n=115")
End Test

Test "14_byref_missing_at_callsite_err.vb — Reject missing ByRef at call site"
  Assert RunVb("tests/byref/14_byref_missing_at_callsite_err.vb", "") <> 0
  Assert OutHas("expects ByRef at call site")
End Test

Test "15_byref_on_byval_param_err.vb — Reject ByRef passed to ByVal param"
  Assert RunVb("tests/byref/15_byref_on_byval_param_err.vb", "") <> 0
  Assert OutHas("is ByVal and cannot be passed ByRef")
End Test

Test "16_byref_rvalue_err.vb — Reject ByRef on non-lvalue literal"
  Assert RunVb("tests/byref/16_byref_rvalue_err.vb", "") <> 0
  Assert OutHas("ByRef argument must be a variable")
End Test
