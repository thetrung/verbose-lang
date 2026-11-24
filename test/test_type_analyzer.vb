' Semantic Type Analyzer (--ta) regression suite.

Include "harness.vb"

Test "01_ta_valid_features.vb — Valid features with --ta"
  Assure RunVb("test/type_analyzer/01_ta_valid_features.vb", "--ta") = 0
  Assure OutHas("[OK] Type Analyzer")
  Assure OutHas("ui=0 up=0.000000 p=1.500000,2.500000,9999 gv=7 ok=1 fl=0 b=65")
End Test

Test "02_ta_return_mismatch_err.vb — Reject return type mismatch"
  Assure RunVb("test/type_analyzer/02_ta_return_mismatch_err.vb", "--ta") <> 0
  Assure OutHas("Semantic Type Error")
  Assure OutHas("Return type mismatch")
End Test

Test "03_ta_sub_returns_val_err.vb — Reject Sub returning a value"
  Assure RunVb("test/type_analyzer/03_ta_sub_returns_val_err.vb", "--ta") <> 0
  Assure OutHas("Semantic Type Error")
  Assure OutHas("Sub cannot return a value")
End Test

Test "04_ta_func_bare_return_err.vb — Reject bare Return in non-void Function"
  Assure RunVb("test/type_analyzer/04_ta_func_bare_return_err.vb", "--ta") <> 0
  Assure OutHas("Semantic Type Error")
  Assure OutHas("Function must return a value")
End Test

Test "05_ta_binop_mismatch_err.vb — Reject binary op type mismatch"
  Assure RunVb("test/type_analyzer/05_ta_binop_mismatch_err.vb", "--ta") <> 0
  Assure OutHas("Semantic Type Error")
  Assure OutHas("Type mismatch in binary operation")
End Test

Test "06_ta_float_bitwise_err.vb — Reject bitwise op on floating point"
  Assure RunVb("test/type_analyzer/06_ta_float_bitwise_err.vb", "--ta") <> 0
  Assure OutHas("Semantic Type Error")
  Assure OutHas("Unsupported bitwise operation on floating point")
End Test

Test "07_ta_arity_mismatch_err.vb — Reject wrong argument count"
  Assure RunVb("test/type_analyzer/07_ta_arity_mismatch_err.vb", "--ta") <> 0
  Assure OutHas("Semantic Type Error")
  Assure OutHas("expects 2 argument(s), got 1")
End Test

Test "08_ta_arg_type_mismatch_err.vb — Reject wrong argument type"
  Assure RunVb("test/type_analyzer/08_ta_arg_type_mismatch_err.vb", "--ta") <> 0
  Assure OutHas("Semantic Type Error")
  Assure OutHas("type mismatch: expected Vec, got Boolean")
End Test

Test "09_ta_undef_func_err.vb — Reject undefined function call"
  Assure RunVb("test/type_analyzer/09_ta_undef_func_err.vb", "--ta") <> 0
  Assure OutHas("Semantic Type Error")
  Assure OutHas("Undefined function or constructor: NoSuchFunc")
End Test

Test "10_ta_duplicate_dim_err.vb — Reject duplicate local Dim in same scope"
  Assure RunVb("test/type_analyzer/10_ta_duplicate_dim_err.vb", "--ta") <> 0
  Assure OutHas("Semantic Type Error")
  Assure OutHas("is already declared")
End Test

Test "11_ta_nonbool_if_cond_err.vb — Reject struct value as If condition"
  Assure RunVb("test/type_analyzer/11_ta_nonbool_if_cond_err.vb", "--ta") <> 0
  Assure OutHas("Semantic Type Error")
  Assure OutHas("If condition must be Boolean")
End Test

Test "12_ta_with_field_type_err.vb — Reject mismatched field type in With initializer"
  Assure RunVb("test/type_analyzer/12_ta_with_field_type_err.vb", "--ta") <> 0
  Assure OutHas("Semantic Type Error")
  Assure OutHas("Type mismatch for field 'a'")
End Test
