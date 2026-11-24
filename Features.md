# VerBose-Lang — Implemented Features

VerBose-Lang is a brace-free, VB6/VB.NET-inspired systems programming language compiled directly to LLVM IR without a garbage collector.

## Language & Runtime Features

- **Types & Expressions**: `Byte`, `Short`, `Integer`, `Long`, `Single`, `Double`, `Boolean`, `Pointer`, `Nothing`, hex literals (`0x...`), unary negation (`-x`), contextual literal typing, arithmetic/comparison/logical/bitwise operators (`+`, `-`, `*`, `/`, `Mod`, `=`, `<>`, `<`, `>`, `<=`, `>=`, `And`, `Or`, `Xor`, `Not`, `Shl`, `Shr`).
- **Control Flow**: `If / Then / Else / End If`, `While ... Do / End While`, `For ... To / End For`, multi-value `Select Case ... Case Else / End Select` with early `Return`.
- **Functions & Parameters**: `Function` / `Sub`, forward registration, `ByVal` / `ByRef` parameters, explicit call-site `ByRef` (`foo(ByRef x)`), ABI-aligned struct return values.
- **Structs & Enums**: `Structure` (recursive ABI-aligned sizing, nested structs, deep `.` field access and assignment through `ByRef`), `Enum` (lowered to `i32`), `With ( field = expr, ... )` named initializers, positional struct constructors.
- **Authentic VB `()` Arrays**: Fixed arrays `Dim a(N) As T`, unsized slices `a() As T`, brace-free parenthesized array literals `()`, `(42)`, `(1, 2, 3)` (including inferred length `Dim b() As Integer = (1, 2, 3, 4)`), `a(i)` indexing, `Pointer` byte indexing, `ByVal`/`ByRef` array parameters, and `Len()`.
- **Static, Heap & Deterministic Arena Memory**: Module-level and function-local `Static` globals, `SizeOf(Type)`, `HeapAlloc` / `HeapRealloc` / `HeapAllocBytes` / `HeapFree`, and first-class deterministic `Arena` bump allocator (`ArenaCreate`, `ArenaFromBuffer`, `ArenaAlloc`, `ArenaAllocBytes`, `ArenaReset`, `ArenaMark`, `ArenaRewind`, `ArenaUsed`, `ArenaRemaining`, `ArenaCapacity`, `ArenaDestroy`, `IsNull`).
- **Modules & Encapsulation**: `Include "path.vb"`, `-I` search directories, `.`/`..` path normalization, circular-include detection, diamond include-once deduplication, `Public` / `Private` / `Default` visibility, and module-scoped `Private` isolation.
- **Native C Interop (FFI)**: `Declare Function` / `Sub`, variadic `...`, `ByRef` out-pointers, SysV x86-64 ByVal struct ABI lowering (`Color`, `Vector2`, `Rectangle`, `>16B` `byval`), string escapes (`\n`, `\r`, `\t`, `\"`, `\\`, `\0`), plus a working Raylib 5.x demo in [`examples/raylib/`](examples/raylib/).

## Compiler, Tooling & CI

- **100% Self-Hosted Compiler (`src/`)**: 8-module compiler (`verbose.vb`, `token.vb`, `lexer.vb`, `ast.vb`, `printer.vb`, `parser.vb`, `type_analyzer.vb`, `codegen.vb`) backed by a single 32 MiB deterministic `Arena`. Verified to a byte-for-byte Stage 3 self-hosting fixpoint and passing **105/105** tests (`make test`). The original OCaml Stage 0 bootstrap compiler is preserved on the `stage0` git branch.
- **Semantic Type Analyzer & Inspection**: Automatic semantic type checking (`--ta` verbose output, `--no-ta` opt-out), AST pretty-printer (`--ast`), and LLVM IR inspection (`--ir`).
- **Direct Compile-and-Run CLI**: `./verbose file.vb` type-checks, emits LLVM IR, compiles with `clang -O2`, and executes directly by default, with full flag support (`--emit-only`, `--no-run`, `-o`, `-I`, `-l`, `-L`, `-W`, extra `.c/.o/.a` inputs, `--help`, `--version`).
- **Packaging & CI**: `make install` / `uninstall` / `dist` packaging plus GitHub Actions CI (`.github/workflows/ci.yml`) and automated Linux x86-64 GitHub Releases (`.github/workflows/release.yml`).
