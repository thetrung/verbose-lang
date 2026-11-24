' Include module system & Private encapsulation regression suite.

Include "harness.vb"

Test "01_include_basic.vb — Basic relative Include of struct + struct-returning function"
  Assure RunVb("tests/modules/01_include_basic.vb", "") = 0
  Assure OutHas("sum=42 pt=13,24")
End Test

Test "02_diamond_and_nested.vb — Transitive nested & diamond Include deduplication"
  Assure RunVb("tests/modules/02_diamond_and_nested.vb", "") = 0
  Assure OutHas("a=1 b=3 shared=22")
End Test

Test "03_private_isolation.vb — Module Private encapsulation across 3 files"
  Assure RunVb("tests/modules/03_private_isolation.vb", "") = 0
  Assure OutHas("alpha=13 beta=42 local=104")
End Test

Test "04_include_search_dir.vb — Include resolution via -I search directory"
  Assure RunVb("tests/modules/04_include_search_dir.vb", "-I tests/modules/common") = 0
  Assure OutHas("incdir=25")
End Test

Test "05_private_func_denied_err.vb — Reject cross-module call to Private function"
  Assure RunVb("tests/modules/05_private_func_denied_err.vb", "") <> 0
  Assure OutHas("Cannot access Private function 'helper'")
End Test

Test "06_private_global_denied_err.vb — Reject cross-module read of Private Static global"
  Assure RunVb("tests/modules/06_private_global_denied_err.vb", "") <> 0
  Assure OutHas("Cannot access Private symbol 'secret_factor'")
End Test

Test "07_missing_include_err.vb — Reject missing Included file"
  Assure RunVb("tests/modules/07_missing_include_err.vb", "") <> 0
  Assure OutHas("Included file not found")
End Test

Test "08_circular_include_err.vb — Detect and reject circular Include cycle"
  Assure RunVb("tests/modules/08_circular_include_err.vb", "") <> 0
  Assure OutHas("Circular Include detected")
End Test
