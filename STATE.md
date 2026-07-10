### STATE
```
We are building "verbose-lang", a VB.NET/VB6-inspired systems programming language compiled to LLVM IR via an OCaml text-patching string generator backend (no native LLVM bindings dependency, targeting Apple macOS/arm64, Linux/X86-64, baremetal/ARM-M compatibility). 
Here is our current state:

1. Lexer/Parser: Built via ocamllex and Menhir. Supports conditional blocks, while loops, for loops (fully evaluated bodies), multi-variable Select Case matching, and Case Else fallbacks. Resolves shift/reduce conflicts via %nonassoc ID and an explicit recursive case_branches rule structure.

2. AST: Single source of truth in ast.ml tracking primitive data types, custom StructType(name).

3. Codegen: Emits raw string-concatenated LLVM assembly text with modern opaque pointers (ptr). Handles proper 8-byte boundaries for pointers, dynamically records global string constants (e.g. for printf and file paths like fopen), manages unique basic block label counters, and calculates direct struct field memory byte offsets via getelementptr i8. It compiles cleanly with llc + clang and executes with 0 segmentation faults.

4. Features List (Just DONE):
- FieldAccess + FieldAssign DOT notation
- Negative/Hex Short, Long, Single, Double Number Support.
- Structure Size Compute

5. TODO :
- Enums 
- Arrays
- Public/Private
- Declare/Include
- Example with Raylib
- Arena Allocator : As built-in memory management
- Analyzer : Type Checking & Semantic Analysis
- Bootstrapping : Write a Compiler in VerBose syntax to begin bootstrapping the language.
