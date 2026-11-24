### STATE
```
We are building "verbose-lang", a VB.NET/VB6-inspired systems programming language compiled to LLVM IR via an OCaml text-patching string generator backend (no native LLVM bindings dependency, targeting Apple macOS/arm64, Linux/X86-64, baremetal/ARM-M compatibility).
Here is our current state:

1. Lexer/Parser: Built via ocamllex and Menhir. Supports conditional blocks (If/Then/Else), While ... Do ... End While loops, For ... To ... End For loops, multi-value Select Case matching with Case Else fallbacks, Structure, Enum, Function, Sub, Public visibility, and ByVal/ByRef parameters. Resolves shift/reduce conflicts via explicit recursive case_branches rules.

2. AST: Single source of truth in lib/ast.ml tracking primitive types (Byte, Short, Integer, Long, Single, Double, Boolean, Pointer, Nothing) and Custom(name) struct/enum types.

3. Codegen: Emits raw string-concatenated LLVM assembly text with opaque pointers (ptr), compiled directly via clang -o2. Dynamically records global string constants, manages unique registers/labels, computes 8-byte aligned recursive struct sizes, resolves arbitrarily nested struct field reads and writes via getelementptr + @llvm.memcpy, tracks per-function parameter passing modes (ByVal/ByRef) at call sites for structs, primitives, and struct fields, and clears per-function variable/type tables cleanly.

4. Features List (DONE):
- FieldAccess + Nested FieldAssign DOT notation (arbitrary depth, incl. through ByRef)
- Negative/Hex Short, Long, Single, Double Number Support
- Recursive Structure Size Compute & Nested/Mixed Structs
- Enum (lowered to i32, safe inside structs)
- Allow un-initialized Dim
- Function/Sub Call & ReturnType (with forward registration)
- Boolean & Bitwise/Comparison Operators
- ByVal/ByRef Parameters & Call-Site Lowering (structs, primitives, struct fields, chained forwarding)
- Public/Internal Linkage
- Isolated ByRef Regression Test Suite (13/13 runtime tests in test/byref + test/run_byref_tests.py)
- Clean Build Artifacts via make clean (incl. test/byref outputs)

5. TODO:
- Type Analyzer (--ta): Sync with codegen (register uninit Dim, struct ctor types, literal widening, run before codegen)
- Lexer/Parser Gaps: Add Private keyword, Declare extern FFI syntax (fixes examples/read_file.vb), and With initializer syntax (fixes examples/structure2.vb)
- Arrays: Index/Access memory
- Static/Heap: Global allocator/variable
- Arena Allocator: Built-in deterministic memory management
- Include: Modules & project structure
- Raylib: Write demo to refine C interop & codegen
- Bootstrapping: Write a compiler in VerBose syntax
```
