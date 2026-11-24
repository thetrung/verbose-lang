### STATE
```
We are building "verbose-lang", a VB.NET/VB6-inspired systems programming language compiled to LLVM IR via an OCaml text-patching string generator backend (no native LLVM bindings dependency, targeting Apple macOS/arm64, Linux/X86-64, baremetal/ARM-M compatibility).
Here is our current state:

1. Lexer/Parser: Built via ocamllex and Menhir. Supports conditional blocks (If/Then/Else), While ... Do ... End While loops, For ... To ... End For loops, multi-value Select Case matching with Case Else fallbacks, Structure, Enum, Function, Sub, Public/Private visibility, Declare extern FFI declarations (incl. variadic ...), With { ... } named struct initializers, fixed-size & unsized Arrays (`arr[N]`, `arr[]`, `[a, b, c]`, `arr[i]`), module-level & function-local Static variables, SizeOf/HeapAlloc/HeapRealloc/HeapFree, built-in Arena type & ArenaAlloc, ByVal/ByRef parameters, explicit call-site ByRef/ByVal argument syntax (e.g. foo(ByRef x)), and inline/EOF comments. Resolves shift/reduce conflicts via explicit recursive case_branches rules.

2. AST: Single source of truth in lib/ast.ml tracking primitive types (Byte, Short, Integer, Long, Single, Double, Boolean, Pointer, Arena, Nothing), Custom(name) struct/enum types, Array(elem, size) types, per-argument param_mode on calls, GlobalVar/StaticDim, ExternDecl FFI signatures, StructInit (With { ... }), ArrayLit/IndexAccess/IndexAssign, SizeOf/HeapAlloc/HeapRealloc, and ArenaAlloc.

3. Codegen: Emits raw string-concatenated LLVM assembly text with opaque pointers (ptr), compiled directly via clang -o2. Dynamically records global string constants and Static globals, manages unique registers/labels, computes ABI-aligned recursive struct & array sizes, resolves arbitrarily nested struct field & array index reads/writes via getelementptr + @llvm.memcpy, compiles zero-initialized named With { ... } struct literals and fixed-size arrays, supports Pointer byte indexing and Len()/SizeOf()/IsNull(), provides zero-overhead typed Heap allocation (calloc/realloc/free) and a lazy-emitted built-in 8-byte-aligned Arena bump allocator (heap-backed & zero-malloc Static/stack buffer-backed with Mark/Rewind/Reset/Destroy & deterministic OOM), registers For loop counter variables, emits deduplicated external C FFI declarations (including variadic ... calls with float promotion and ByRef out-pointers), enforces matching ByVal/ByRef modes between call sites and definitions, and clears per-function variable/type tables cleanly.

4. Features List (DONE):
- FieldAccess + Nested FieldAssign DOT notation (arbitrary depth, incl. through ByRef & arrays)
- Negative/Hex Short, Long, Single, Double Number Support
- ABI-Aligned Recursive Structure Size Compute & Nested/Mixed Structs (`SizeOf(T)`)
- Enum (lowered to i32, safe inside structs)
- Allow un-initialized Dim
- Named Struct Initializers via `With { field = expr, ... }` (multi-line, partial zero-init, nested, expression-level)
- Fixed-Size & Unsized Arrays (`Dim a[N]`, `Dim a[] = [...]`, struct-embedded arrays, `arr[i]` read/write, `Pointer` byte indexing, `ByVal`/`ByRef` array params & elements, `Len()`)
- Module-Level & Function-Local `Static` Globals (scalars, structs, arrays, strings, global heap slices, local shadowing)
- Typed Heap Allocator (`HeapAlloc`, `HeapRealloc`, `HeapAllocBytes`, `HeapFree`, `IsNull`)
- Built-in Deterministic Arena Allocator (`Arena`, `ArenaCreate`, `ArenaFromBuffer`, `ArenaAlloc`, `ArenaAllocBytes`, `ArenaReset`, `ArenaMark`/`ArenaRewind`, `ArenaUsed`/`ArenaRemaining`/`ArenaCapacity`, `ArenaDestroy`)
- Function/Sub Call & ReturnType (with forward registration)
- Boolean & Bitwise/Comparison Operators
- For Loop Counter Typing & C Stdio Builtins (fopen, fclose, getc, putc, printf)
- External C FFI Declarations via `Declare Function/Sub` (incl. variadic `...`, ByRef out-pointers, builtin deduplication)
- ByVal/ByRef Parameters & Explicit Call-Site `ByRef` (`foo(ByRef x)`) with strict signature enforcement (structs, primitives, struct fields, arrays, arenas)
- Public/Private Linkage (`Public` / `Private` / default internal)
- Inline & EOF Comment Support + Accurate Lexer Error Reporting
- Regression Test Suites: ByRef (16/16), Syntax/FFI/With (10/10), Arrays (12/12), Static/Heap (11/11), Arena (11/11) — 60/60 passing
- Complete Example Suite Passing (14/14: prototype.vb + 13 examples/*.vb incl. arrays.vb, static_heap.vb, arena.vb)
- Clean Build Artifacts via make clean (incl. all test/* outputs)

5. TODO:
- Type Analyzer (--ta): Sync with codegen (register uninit Dim, struct ctor types, literal widening, run before codegen)
- Include: Modules & project structure
- Raylib: Write demo to refine C interop & codegen
- Bootstrapping: Write a compiler in VerBose syntax
```
