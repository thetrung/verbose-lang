' VerBose Self-Hosting Compiler (verbose) - Shared Diagnostics, Messages & LLVM IR Templates

' --- Compiler Version & CLI Driver Messages ---
Public Static MSG_CLI_VERSION As Pointer = "verbose 0.4.0 (VerBose-Lang Self-Hosted Compiler)\n"
Public Static MSG_CLI_USAGE As Pointer = "Usage: verbose <filename>.vb [options]\n       verbose test [file.vb | dir] [options]\n  test         Run built-in Test/Assert blocks in a .vb file or directory (default: tests)\n  --test       Alias for 'verbose test'\n  --ast        Show parsed AST\n  --ir         Show generated LLVM-IR\n  --ta         Verbose type-analyzer pass (runs automatically before codegen)\n  --no-ta      Skip semantic type-analyzer pass\n  --emit-only  Emit .ll only (skip clang & execution)\n  --no-run     Compile binary with clang without executing it\n  -I <dir>     Add module Include search directory\n  -o <path>    Specify output binary path (or .ll path with --emit-only)\n  -l<lib>      Link external C library (e.g. -lm, -lraylib)\n  -L<dir>      Add library search path for clang\n  <file.c/.o>  Additional C/object files to compile and link with clang\n  -h, --help   Show this help message\n  --version    Show compiler version\n"
Public Static MSG_CLI_PARSE_ABORT As Pointer = "verbose: aborting due to %d parse error(s)\n"
Public Static MSG_CLI_AST_HEADER As Pointer = "\n --------- Parsed Source Code ---------\n"
Public Static MSG_CLI_TA_OK As Pointer = " [OK] Type Analyzer: semantic check passed.\n"
Public Static MSG_CLI_IR_HEADER As Pointer = "\n ------- GENERATED LLVM IR CODE -------\n"
Public Static MSG_CLI_CLANG_STATUS As Pointer = "Status: clang = %d\n"
Public Static MSG_CLI_ERR_O_ARG As Pointer = "Error: -o requires a path argument\n"
Public Static MSG_CLI_ERR_I_ARG As Pointer = "Error: -I requires a directory argument\n"
Public Static MSG_CLI_ERR_UNKNOWN_OPT As Pointer = "Unknown option: %s\n"
Public Static MSG_CLI_ERR_UNEXPECTED_ARG As Pointer = "Unexpected argument: %s\n"
Public Static MSG_CLI_TEST_NO_BLOCKS As Pointer = "verbose test error: no Test blocks found in '%s'\n"
Public Static MSG_CLI_TEST_SCAN_FAIL As Pointer = "verbose test error: failed to scan directory '%s'\n"
Public Static MSG_CLI_TEST_NO_FILES As Pointer = "verbose test error: no test_*.vb or *_test.vb files found in '%s'\n"
Public Static MSG_CLI_TEST_RUNNING As Pointer = "\n=== Running %s ===\n"
Public Static MSG_CLI_TEST_SEP As Pointer = "\n========================================\n"
Public Static MSG_CLI_TEST_SUMMARY As Pointer = "Test suites: %d/%d passed, %d failed.\n"

' --- Lexer & Module Loader Diagnostics ---
Public Static MSG_LEX_INC_NOT_FOUND As Pointer = "Module Load Error: Included file not found: '%s'\n"
Public Static MSG_LEX_CIRCULAR_INC As Pointer = "Module Load Error: Circular Include detected: '%s'\n"
Public Static MSG_LEX_OPEN_FAIL As Pointer = "verbose error: cannot open file '%s'\n"
Public Static MSG_LEX_OOM As Pointer = "verbose error: arena OOM reading file\n"
Public Static MSG_LEX_UNEXPECTED_CHAR As Pointer = "verbose lexer error: unexpected char %d at line %d\n"

' --- Parser Diagnostics ---
Public Static MSG_PARSE_EXPECT As Pointer = "verbose parse error: expected token %d, got %d at line %d\n"
Public Static MSG_PARSE_EXPECT_TYPE As Pointer = "verbose parse error: expected type, got token %d at line %d\n"
Public Static MSG_PARSE_UNEXPECTED_PRIMARY As Pointer = "verbose parse error: unexpected token %d in primary at line %d\n"
Public Static MSG_PARSE_INVALID_ASSIGN As Pointer = "verbose parse error: invalid assignment target at line %d\n"

' --- Type Analyzer Shared Diagnostics ---
Public Static MSG_TA_ARRAY_SIZE_POS As Pointer = "Semantic Type Error: Array size must be greater than 0\n"
Public Static MSG_TA_PRIVATE_TYPE As Pointer = "Semantic Type Error: Cannot access Private type '"
Public Static MSG_TA_UNKNOWN_TYPE As Pointer = "Semantic Type Error: Unknown type '"
Public Static MSG_TA_FROM_MOD_SUFFIX As Pointer = "' from included module\n"
Public Static MSG_TA_BYREF_LVALUE As Pointer = "Semantic Type Error: ByRef argument must be a variable, struct field, or array element\n"
Public Static MSG_TA_INDEX_INT As Pointer = "Semantic Type Error: Array index must be an integer type\n"
Public Static MSG_TA_INDEX_NON_ARR As Pointer = "Semantic Type Error: Cannot index non-array, non-pointer expression\n"
Public Static MSG_TA_BINOP_MISMATCH As Pointer = "Semantic Type Error: Type mismatch in binary operation: "
Public Static MSG_TA_FLOAT_BITWISE As Pointer = "Semantic Type Error: Unsupported bitwise operation on floating point values\n"
Public Static MSG_TA_STR_CONCAT_ERR As Pointer = "Semantic Type Error: String concatenation '&' requires Pointer or Byte array operands\n"
Public Static MSG_TA_STATIC_ARR_OVERFLOW As Pointer = "Semantic Type Error: Static array initializer has %d elements, exceeding size %d\n"
Public Static MSG_TA_STATIC_STRUCT_MISMATCH As Pointer = "Semantic Type Error: Static struct type mismatch\n"
Public Static MSG_TA_STATIC_CONST_ERR As Pointer = "Semantic Type Error: Static variable initializer must be a compile-time constant\n"
Public Static MSG_TA_BUILTIN_NO_BYREF As Pointer = "Semantic Type Error: Built-in function does not accept ByRef arguments\n"

' --- Codegen Diagnostics ---
Public Static MSG_CG_OPEN_FAIL As Pointer = "verbose codegen error: cannot open output file\n"
Public Static MSG_CG_UNDEF_VAR As Pointer = "verbose codegen error: undefined variable\n"
Public Static MSG_CG_INVALID_LVAL As Pointer = "verbose codegen error: invalid lvalue\n"

' --- LLVM IR Module & Runtime Templates (emitted via fputs) ---
Public Static IR_MODULE_HEADER As Pointer = "; Generated by verbose (VerBose Self-Hosting Compiler)\n\n"
Public Static IR_ARENA_STRUCT_DEF As Pointer = "%struct.__vb_arena = type { ptr, i64, i64 }\n\n"
Public Static IR_MEMCPY_DECL As Pointer = "declare void @llvm.memcpy.p0.p0.i64(ptr nocapture writeonly, ptr nocapture readonly, i64, i1 immarg)\n\n"

Public Static IR_ARENA_RT_INIT_HEAP As Pointer = "declare void @llvm.memset.p0.i64(ptr nocapture writeonly, i8, i64, i1 immarg)\n\ndefine internal void @__vb_arena_init_heap(ptr %a, i64 %cap) {\nentry:\n  %valid = icmp sgt i64 %cap, 0\n  br i1 %valid, label %alloc_blk, label %zero_blk\nalloc_blk:\n  %mem = call ptr @calloc(i64 1, i64 %cap)\n  %ok = icmp ne ptr %mem, null\n  br i1 %ok, label %store_ok, label %zero_blk\nstore_ok:\n  %p0 = getelementptr inbounds %struct.__vb_arena, ptr %a, i32 0, i32 0\n  store ptr %mem, ptr %p0, align 8\n  %p1 = getelementptr inbounds %struct.__vb_arena, ptr %a, i32 0, i32 1\n  store i64 %cap, ptr %p1, align 8\n  %p2 = getelementptr inbounds %struct.__vb_arena, ptr %a, i32 0, i32 2\n  store i64 0, ptr %p2, align 8\n  ret void\nzero_blk:\n  store %struct.__vb_arena zeroinitializer, ptr %a, align 8\n  ret void\n}\n\n"
Public Static IR_ARENA_RT_INIT_BUF As Pointer = "define internal void @__vb_arena_init_buf(ptr %a, ptr %buf, i64 %cap) {\nentry:\n  %valid = icmp sgt i64 %cap, 0\n  br i1 %valid, label %store_ok, label %zero_blk\nstore_ok:\n  %p0 = getelementptr inbounds %struct.__vb_arena, ptr %a, i32 0, i32 0\n  store ptr %buf, ptr %p0, align 8\n  %p1 = getelementptr inbounds %struct.__vb_arena, ptr %a, i32 0, i32 1\n  store i64 %cap, ptr %p1, align 8\n  %p2 = getelementptr inbounds %struct.__vb_arena, ptr %a, i32 0, i32 2\n  store i64 0, ptr %p2, align 8\n  ret void\nzero_blk:\n  store %struct.__vb_arena zeroinitializer, ptr %a, align 8\n  ret void\n}\n\n"
Public Static IR_ARENA_RT_ALLOC As Pointer = "define internal ptr @__vb_arena_alloc(ptr %a, i64 %size) {\nentry:\n  %le0 = icmp sle i64 %size, 0\n  br i1 %le0, label %oom, label %align_blk\nalign_blk:\n  %plus7 = add i64 %size, 7\n  %aligned = and i64 %plus7, -8\n  %cap_ptr = getelementptr inbounds %struct.__vb_arena, ptr %a, i32 0, i32 1\n  %cap = load i64, ptr %cap_ptr, align 8\n  %off_ptr = getelementptr inbounds %struct.__vb_arena, ptr %a, i32 0, i32 2\n  %off = load i64, ptr %off_ptr, align 8\n  %new_off = add i64 %off, %aligned\n  %wrap = icmp ult i64 %new_off, %off\n  %ovf = icmp ugt i64 %new_off, %cap\n  %fail = or i1 %wrap, %ovf\n  br i1 %fail, label %oom, label %ok\nok:\n  %base_slot = getelementptr inbounds %struct.__vb_arena, ptr %a, i32 0, i32 0\n  %base = load ptr, ptr %base_slot, align 8\n  %isnull = icmp eq ptr %base, null\n  br i1 %isnull, label %oom, label %commit\ncommit:\n  store i64 %new_off, ptr %off_ptr, align 8\n  %res = getelementptr inbounds i8, ptr %base, i64 %off\n  call void @llvm.memset.p0.i64(ptr align 8 %res, i8 0, i64 %aligned, i1 false)\n  ret ptr %res\noom:\n  ret ptr null\n}\n\n"
Public Static IR_ARENA_RT_OPS As Pointer = "define internal void @__vb_arena_reset(ptr %a) {\n  %off_ptr = getelementptr inbounds %struct.__vb_arena, ptr %a, i32 0, i32 2\n  store i64 0, ptr %off_ptr, align 8\n  ret void\n}\n\ndefine internal i64 @__vb_arena_used(ptr %a) {\n  %off_ptr = getelementptr inbounds %struct.__vb_arena, ptr %a, i32 0, i32 2\n  %off = load i64, ptr %off_ptr, align 8\n  ret i64 %off\n}\n\ndefine internal i64 @__vb_arena_capacity(ptr %a) {\n  %cap_ptr = getelementptr inbounds %struct.__vb_arena, ptr %a, i32 0, i32 1\n  %cap = load i64, ptr %cap_ptr, align 8\n  ret i64 %cap\n}\n\ndefine internal i64 @__vb_arena_remaining(ptr %a) {\n  %cap_ptr = getelementptr inbounds %struct.__vb_arena, ptr %a, i32 0, i32 1\n  %cap = load i64, ptr %cap_ptr, align 8\n  %off_ptr = getelementptr inbounds %struct.__vb_arena, ptr %a, i32 0, i32 2\n  %off = load i64, ptr %off_ptr, align 8\n  %rem = sub i64 %cap, %off\n  ret i64 %rem\n}\n\n"
Public Static IR_ARENA_RT_REWIND_DESTROY As Pointer = "define internal void @__vb_arena_rewind(ptr %a, i64 %mark) {\nentry:\n  %off_ptr = getelementptr inbounds %struct.__vb_arena, ptr %a, i32 0, i32 2\n  %off = load i64, ptr %off_ptr, align 8\n  %nonneg = icmp sge i64 %mark, 0\n  %le_off = icmp sle i64 %mark, %off\n  %ok = and i1 %nonneg, %le_off\n  br i1 %ok, label %do_rewind, label %done\ndo_rewind:\n  store i64 %mark, ptr %off_ptr, align 8\n  br label %done\ndone:\n  ret void\n}\n\ndefine internal void @__vb_arena_destroy(ptr %a) {\nentry:\n  %base_slot = getelementptr inbounds %struct.__vb_arena, ptr %a, i32 0, i32 0\n  %base = load ptr, ptr %base_slot, align 8\n  %nonnull = icmp ne ptr %base, null\n  br i1 %nonnull, label %do_free, label %clear\ndo_free:\n  call void @free(ptr %base)\n  br label %clear\nclear:\n  store %struct.__vb_arena zeroinitializer, ptr %a, align 8\n  ret void\n}\n\n"

Public Static IR_ASSERT_TEST_DECLS As Pointer = "@__vb_test_cur_fail = internal global i32 0, align 4\n@__vb_assert_fmt = private unnamed_addr constant [27 x i8] c\"  [ASSERT FAILED] line %d\\0A\\00\", align 1\n@__vb_assert_msg_fmt = private unnamed_addr constant [31 x i8] c\"  [ASSERT FAILED] line %d: %s\\0A\\00\", align 1\n@__vb_test_pass_fmt = private unnamed_addr constant [11 x i8] c\"[PASS] %s\\0A\\00\", align 1\n@__vb_test_fail_fmt = private unnamed_addr constant [11 x i8] c\"[FAIL] %s\\0A\\00\", align 1\n@__vb_test_sum_fmt = private unnamed_addr constant [42 x i8] c\"\\0ASummary: %d/%d tests passed, %d failed.\\0A\\00\", align 1\n\n"
Public Static IR_ASSERT_TEST_FN As Pointer = "define internal void @__vb_assert_fail(i32 %line, ptr %msg) {\nentry:\n  store i32 1, ptr @__vb_test_cur_fail, align 4\n  %isnull = icmp eq ptr %msg, null\n  br i1 %isnull, label %no_msg, label %has_msg\nno_msg:\n  call i32 (ptr, ...) @printf(ptr @__vb_assert_fmt, i32 %line)\n  br label %done\nhas_msg:\n  call i32 (ptr, ...) @printf(ptr @__vb_assert_msg_fmt, i32 %line, ptr %msg)\n  br label %done\ndone:\n  call i32 @fflush(ptr null)\n  ret void\n}\n\n"
Public Static IR_ASSERT_NORM_DECLS As Pointer = "@__vb_assert_fmt = private unnamed_addr constant [25 x i8] c\"[ASSERT FAILED] line %d\\0A\\00\", align 1\n@__vb_assert_msg_fmt = private unnamed_addr constant [29 x i8] c\"[ASSERT FAILED] line %d: %s\\0A\\00\", align 1\n\n"
Public Static IR_ASSERT_NORM_FN As Pointer = "define internal void @__vb_assert_fail(i32 %line, ptr %msg) {\nentry:\n  %isnull = icmp eq ptr %msg, null\n  br i1 %isnull, label %no_msg, label %has_msg\nno_msg:\n  call i32 (ptr, ...) @printf(ptr @__vb_assert_fmt, i32 %line)\n  br label %done\nhas_msg:\n  call i32 (ptr, ...) @printf(ptr @__vb_assert_msg_fmt, i32 %line, ptr %msg)\n  br label %done\ndone:\n  call i32 @fflush(ptr null)\n  call void @exit(i32 1)\n  ret void\n}\n\n"
Public Static IR_TEST_MAIN_PROLOGUE As Pointer = "define i32 @main() {\nentry:\n  %passed = alloca i32, align 4\n  %failed = alloca i32, align 4\n  store i32 0, ptr %passed, align 4\n  store i32 0, ptr %failed, align 4\n"
Public Static IR_TEST_MAIN_EPILOGUE As Pointer = "  %tot_p = load i32, ptr %passed, align 4\n  %tot_f = load i32, ptr %failed, align 4\n  %tot = add i32 %tot_p, %tot_f\n  call i32 (ptr, ...) @printf(ptr @__vb_test_sum_fmt, i32 %tot_p, i32 %tot, i32 %tot_f)\n  call i32 @fflush(ptr null)\n  %any_fail = icmp ne i32 %tot_f, 0\n  %rc = zext i1 %any_fail to i32\n  ret i32 %rc\n}\n\n"

Public Static IR_STR_CONCAT_RT As Pointer = "define internal ptr @__vb_str_concat(ptr %a, ptr %b) {\nentry:\n  %a_null = icmp eq ptr %a, null\n  br i1 %a_null, label %a_zero, label %a_len\na_zero:\n  br label %check_b\na_len:\n  %la_val = call i64 @strlen(ptr %a)\n  br label %check_b\ncheck_b:\n  %la = phi i64 [ 0, %a_zero ], [ %la_val, %a_len ]\n  %b_null = icmp eq ptr %b, null\n  br i1 %b_null, label %b_zero, label %b_len\nb_zero:\n  br label %alloc\nb_len:\n  %lb_val = call i64 @strlen(ptr %b)\n  br label %alloc\nalloc:\n  %lb = phi i64 [ 0, %b_zero ], [ %lb_val, %b_len ]\n  %sum = add i64 %la, %lb\n  %tot = add i64 %sum, 1\n  %buf = call ptr @calloc(i64 %tot, i64 1)\n  %buf_null = icmp eq ptr %buf, null\n  br i1 %buf_null, label %done, label %copy_a\ncopy_a:\n  %has_a = icmp sgt i64 %la, 0\n  br i1 %has_a, label %do_copy_a, label %copy_b\ndo_copy_a:\n  call void @llvm.memcpy.p0.p0.i64(ptr %buf, ptr %a, i64 %la, i1 false)\n  br label %copy_b\ncopy_b:\n  %has_b = icmp sgt i64 %lb, 0\n  br i1 %has_b, label %do_copy_b, label %done\ndo_copy_b:\n  %dst_b = getelementptr inbounds i8, ptr %buf, i64 %la\n  call void @llvm.memcpy.p0.p0.i64(ptr %dst_b, ptr %b, i64 %lb, i1 false)\n  br label %done\ndone:\n  ret ptr %buf\n}\n\n"
