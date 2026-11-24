' Syntax regression suite (Private, Declare FFI, With initializers, inline comments).

Include "harness.vb"

Test "01_private_visibility.vb — Private Structure/Enum/Function/Sub with internal linkage"
  Assert RunVb("tests/syntax/01_private_visibility.vb", "--ir") = 0
  Assert OutHas("define internal i32 @AddXY")
  Assert OutHas("define internal void @BumpX")
  Assert OutHas("i32 @main()")
  Assert OutHas("pt=15,20 sum=35")
End Test

Test "02_declare_ffi_stdlib.vb — Declare FFI calls to C stdlib"
  Assert RunVb("tests/syntax/02_declare_ffi_stdlib.vb", "") = 0
  Assert OutHas("ffi_puts_ok")
  Assert OutHas("abs=42 strlen=7 atoi=1234")
End Test

Test "03_declare_override_builtins.vb — Declare deduplication with built-in C functions + fclose"
  Assert RunVb("tests/syntax/03_declare_override_builtins.vb", "") = 0
  Assert OutHas("first=39 closed=0")
End Test

Test "04_declare_variadic_and_byref.vb — Declare variadic FFI (sscanf ByRef + snprintf float)"
  Assert RunVb("tests/syntax/04_declare_variadic_and_byref.vb", "") = 0
  Assert OutHas("matched=2 written=15 text=[a=42 b=99 f=3.5]")
End Test

Test "05_with_initializer_basic.vb — With ( ... ) multi-line, single-line, out-of-order, trailing comma"
  Assert RunVb("tests/syntax/05_with_initializer_basic.vb", "") = 0
  Assert OutHas("v1=3.14,2.75,9999999 v2=1.5,4.5,7")
End Test

Test "06_with_partial_and_nested.vb — With partial zero-init, nested With, and reassignment"
  Assert RunVb("tests/syntax/06_with_partial_and_nested.vb", "") = 0
  Assert OutHas("partial=0.00,7.50 init=10:1.25,2.50")
  Assert OutHas("reassigned=20:0.00,7.50")
End Test

Test "07_inline_comments.vb — Inline comments and EOF comment without trailing newline"
  Assert RunVb("tests/syntax/07_inline_comments.vb", "") = 0
  Assert OutHas("out=41")
End Test

Test "08_with_unknown_field_err.vb — Reject unknown field name in With initializer"
  Assert RunVb("tests/syntax/08_with_unknown_field_err.vb", "") <> 0
  Assert OutHas("Structure 'Point' has no field named 'nonexistent'")
End Test

Test "09_with_duplicate_field_err.vb — Reject duplicate field name in With initializer"
  Assert RunVb("tests/syntax/09_with_duplicate_field_err.vb", "") <> 0
  Assert OutHas("Duplicate field 'x' in 'Point' With initializer")
End Test

Test "10_declare_byref_check_err.vb — Enforce ByRef at call site for Declare externs"
  Assert RunVb("tests/syntax/10_declare_byref_check_err.vb", "") <> 0
  Assert OutHas("expects ByRef at call site")
End Test
