### STATE
```
We are building "verbose-lang", a VB.NET/VB6-inspired systems programming language compiled to LLVM IR via an OCaml text-patching string generator backend (no native LLVM bindings dependency, targeting Apple macOS/arm64, Linux/X86-64, baremetal/ARM-M compatibility).
Here is our current state:

1. Lexer/Parser: Built via ocamllex and Menhir. Supports conditional blocks (If/Then/Else), While ... Do ... End While loops, For ... To ... End For loops, multi-value Select Case matching with Case Else fallbacks, Structure, Enum, Function, Sub, Public/Private visibility, Declare extern FFI declarations (incl. variadic ...), With { ... } named struct initializers, ByVal/ByRef parameters, explicit call-site ByRef/ByVal argument syntax (e.g. foo(ByRef x)), and inline/EOF comments. Resolves shift/reduce conflicts via explicit recursive case_branches rules.

2. AST: Single source of truth in lib/ast.ml tracking primitive types (Byte, Short, Integer, Long, Single, Double, Boolean, Pointer, Nothing), Custom(name) struct/enum types, per-argument param_mode on calls, ExternDecl FFI signatures, and StructInit (With { ... }) expressions.

3. Codegen: Emits raw string-concatenated LLVM assembly text with opaque pointers (ptr), compiled directly via clang -o2. Dynamically records global string constants, manages unique registers/labels, computes 8-byte aligned recursive struct sizes, resolves arbitrarily nested struct field reads and writes via getelementptr + @llvm.memcpy, compiles zero-initialized named With { ... } struct literals, registers For loop counter variables, emits deduplicated external C FFI declarations (including variadic ... calls with float promotion and ByRef out-pointers), enforces matching ByVal/ByRef modes between call sites and definitions (for structs, primitives, and struct fields), and clears per-function variable/type tables cleanly.

4. Features List (DONE):
- FieldAccess + Nested FieldAssign DOT notation (arbitrary depth, incl. through ByRef)
- Negative/Hex Short, Long, Single, Double Number Support
- Recursive Structure Size Compute & Nested/Mixed Structs
- Enum (lowered to i32, safe inside structs)
- Allow un-initialized Dim
- Named Struct Initializers via `With { field = expr, ... }` (multi-line, partial zero-init, nested, expression-level)
- Function/Sub Call & ReturnType (with forward registration)
- Boolean & Bitwise/Comparison Operators
- For Loop Counter Typing & C Stdio Builtins (fopen, fclose, getc, putc, printf)
- External C FFI Declarations via `Declare Function/Sub` (incl. variadic `...`, ByRef out-pointers, builtin deduplication)
- ByVal/ByRef Parameters & Explicit Call-Site `ByRef` (`foo(ByRef x)`) with strict signature enforcement (structs, primitives, struct fields, chained forwarding)
- Public/Private Linkage (`Public` / `Private` / default internal)
- Inline & EOF Comment Support + Accurate Lexer Error Reporting
- Regression Test Suites: ByRef (16/16 in test/byref) & Syntax/FFI/With (10/10 in test/syntax)
- Complete Example Suite Passing (11/11: prototype.vb + 10 examples/*.vb incl. byref.vb, structure2.vb, read_file.vb)
- Clean Build Artifacts via make clean (incl. test/byref and test/syntax outputs)

5. TODO:
- Type Analyzer (--ta): Sync with codegen (register uninit Dim, struct ctor types, literal widening, run before codegen)
- Arrays: Index/Access memory
- Static/Heap: Global allocator/variable
- Arena Allocator: Built-in deterministic memory management
- Include: Modules & project structure
- Raylib: Write demo to refine C interop & codegen
- Bootstrapping: Write a compiler in VerBose syntax
```
