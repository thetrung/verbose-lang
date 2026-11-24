# VerBose-Lang — Project State

**VerBose-Lang** is a VB6/VB.NET-inspired systems programming language compiled directly to LLVM IR (no GC, no native LLVM binding dependency, targeting Linux x86-64, macOS arm64, and bare-metal ARM-M) — now **fully self-hosted** via `vbc`, written in VerBose-Lang itself.

---

## Architecture Overview

| Component | Location | Description |
| --- | --- | --- |
| **Self-Hosted Compiler (`vbc`)** | Root (`vbc.vb`, `token.vb`, `lexer.vb`, `ast.vb`, `parser.vb`, `codegen.vb`) | 6-module compiler written in VerBose-Lang using a single 16 MiB deterministic `Arena`. Passes Stage 1 → Stage 2 → Stage 3 byte-for-byte self-hosting fixpoint and **68/68** compatibility tests across all language suites & examples. |
| **Stage 0 Bootstrap Compiler** | `stage0/` (`stage0/bin/`, `stage0/lib/`, `stage0/verbose`) | Original OCaml/ocamllex/Menhir compiler (`stage0/verbose`) with integrated semantic Type Analyzer (`--ta`), CLI flags (`-o`, `-I`, `-l`, `-L`, `--emit-only`, `--no-run`), and 92/92 regression tests (including negative error cases). |
| **Examples & Bindings** | `examples/`, `prototype.vb`, `raylib/` | 14 standalone language examples plus the Raylib 5.x C-interop bouncing-ball demo (`examples/raylib/`). |
| **Test Suites** | `test/` | Automated compatibility & regression runners for both `./vbc` (`make test-vbc`) and `stage0/verbose` (`make test-stage0`). |

---

## Compiler Pipeline

### 1. Lexer & Multi-File Module Loader
- **Self-hosted (`lexer.vb`)**: Single-pool `g_srcPool` byte arena, hex & decimal integers, `Single`/`Double` floating-point literals, escape-aware strings, inline/EOF comments (`'`), `Include "path.vb"` splicing with canonical `.`/`..` path normalization, include-once diamond deduplication, and per-file `modId` tagging.
- **Stage 0 (`stage0/lib/lexer.mll`, `loader.ml`)**: `ocamllex` + Menhir parser (3 S/R conflicts via unified `postfix_expr`) + `-I` search path resolution, circular-include detection, and module-level `Private` alpha-renaming.

### 2. Parser & AST
- **Self-hosted (`ast.vb`, `parser.vb`)**: Arena-allocated flat AST node & child-index pools (`g_nodes()`, `g_kids()`). Supports all statements, expressions, operator precedences (`Or`/`Xor`, `And`, `Not`, comparisons, `Shl`/`Shr`, `+/-`, `* / Mod`, unary `-`), authentic VB `()` array syntax, `With { ... }` named struct initializers, positional struct constructors, and `Declare` FFI signatures (including variadic `...`).
- **Stage 0 (`stage0/lib/ast.ml`, `type_analyzer.ml`)**: Full semantic Type Analyzer running automatically before codegen (`--ta` verbose, `--no-ta` opt-out) validating variable/function/call disambiguation, numeric/pointer assignability, `ByRef` lvalue/mode rules, `Static` constant initializers, `Arena`/`Heap`/`Array` semantics, and cross-module `Private` encapsulation.

### 3. LLVM IR Codegen
- **Self-hosted (`codegen.vb`) & Stage 0 (`stage0/lib/codegen.ml`)**: Emits raw LLVM IR text with opaque pointers (`ptr`) and named SSA registers (`%rN`), compiled to native binaries via `clang -O2`.
- Handles deterministic zero-initialized `Dim`, ABI-aligned struct/array layouts & `memcpy`, struct return values, SysV x86-64 C ABI ByVal struct lowering for `Declare` (`Color` → `i32`, `Vector2` → `<2 x float>`, `Rectangle` → `<2 x float>, <2 x float>`, `>16B` → `ptr byval(%struct.X)`), scalar coercions & variadic `float → double` promotion, IEEE-754 hex `Single` global constants, module-scoped `Private` symbol mangling (`__mod<N>_<name>`), function-local `Static` globals (`@__vb_static_N`), and lazily emitted deterministic `Arena` runtime helpers.

---

## Implemented Features

- [x] **Primitive & Numeric Types**: `Byte`, `Short`, `Integer`, `Long`, `Single`, `Double`, `Boolean`, `Pointer`, `Nothing`, hex literals (`0x...`), unary negation (`-x`), contextual literal typing
- [x] **Operators**: Arithmetic (`+`, `-`, `*`, `/`, `Mod`), Comparisons (`=`, `<>`, `<`, `>`, `<=`, `>=`), Boolean & Bitwise (`And`, `Or`, `Xor`, `Not`, `Shl`, `Shr`)
- [x] **Control Flow**: `If / Then / Else / End If`, `While ... Do / End While`, `For ... To / End For`, multi-value `Select Case ... Case Else / End Select` with early `Return`
- [x] **Functions & Procedures**: `Function` / `Sub`, forward registration, `ByVal` / `ByRef` parameters, explicit call-site `ByRef` (`foo(ByRef x)`), struct return values
- [x] **Composite Types**: `Structure` (recursive ABI-aligned sizing, nested structs, deep `.` field access/assign through `ByRef`), `Enum` (lowered to `i32`), `With { field = expr, ... }` named initializers, positional struct constructors
- [x] **Authentic VB Arrays**: Fixed arrays `Dim arr(N) As T`, unsized slices `arr() As T`, array literals `{1, 2, 3}`, `arr(i)` indexing, `Pointer` byte indexing, `ByVal`/`ByRef` arrays, `Len()`
- [x] **Static & Heap Memory**: Module-level & function-local `Static` globals, `SizeOf(Type)`, `HeapAlloc`, `HeapRealloc`, `HeapAllocBytes`, `HeapFree`
- [x] **Deterministic Arena Allocator**: `Arena` type, `ArenaCreate`, `ArenaFromBuffer`, `ArenaAlloc`, `ArenaAllocBytes`, `ArenaReset`, `ArenaMark`, `ArenaRewind`, `ArenaUsed`, `ArenaRemaining`, `ArenaCapacity`, `ArenaDestroy`, `IsNull`
- [x] **Multi-File Modules & Encapsulation**: `Include "path.vb"`, `.`/`..` path canonicalization, diamond include-once deduplication, `Public`/`Private`/`Default` visibility & module-scoped `Private` isolation
- [x] **Native C Interop (FFI)**: `Declare Function/Sub`, variadic `...`, `ByRef` out-pointers, SysV x86-64 ByVal struct lowering, full string escapes (`\n`, `\r`, `\t`, `\"`, `\\`, `\0`), Raylib 5.x demo (`examples/raylib/`)
- [x] **Full Self-Hosting (`vbc`)**: Stage 1 → Stage 2 → Stage 3 byte-for-byte fixpoint verified (**891 KB** identical IR) + **68/68** compatibility across all test suites and examples

---

## Verification Status

| Suite / Command | Result | Scope |
| --- | --- | --- |
| `make bootstrap` | **8/8 PASS** + Stage 3 fixpoint | `stage0/verbose` → `vbc1` → `./vbc` → `vbc2.ll == vbc3.ll` byte-for-byte identical |
| `make test-vbc` | **68/68 PASS** | `syntax` 7/7, `byref` 13/13, `arrays` 9/9, `static_heap` 9/9, `arena` 8/8, `type_analyzer` 1/1, `modules` 3/3, `c_interop` 4/4, `examples` 14/14 |
| `make test-stage0` | **92/92 PASS** | All 9 Stage 0 suites including negative error & `--ta` semantic checks |

---

## Roadmap / TODO

- [ ] **Self-Hosted Semantic Type Checker & CLI**: Port Stage 0's Type Analyzer diagnostics and CLI flags (`-o`, `-I`, `-l`, `-L`, `--emit-only`, direct `clang` invocation) into `vbc`
- [ ] **Cross-Compilation & Target Triples**: Emit explicit LLVM target triples & data layouts for Linux x86-64, macOS arm64, and bare-metal 32-bit ARM-M (incl. `Volatile` / fixed memory regions)
- [ ] **Standard Library Modules**: Build `std/` modules (`strings`, `math`, `collections`, `io`) using `Include` + `Arena`
- [ ] **Tooling & CI**: Add package/build tooling and GitHub Actions CI workflow running `make test`
- [ ] **Language Extensions**: Structured concurrency (`Async`/`Await`) and low-level address/label primitives
