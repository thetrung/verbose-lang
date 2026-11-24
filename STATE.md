# VerBose-Lang — Project State

**VerBose-Lang** is a VB6/VB.NET-inspired systems programming language compiled directly to LLVM IR (no GC, no native LLVM binding dependency, targeting Linux x86-64, macOS arm64, and bare-metal ARM-M) — now **fully self-hosted** via `./verbose`, written in VerBose-Lang itself.

---

## Architecture Overview

| Component | Location | Description |
| --- | --- | --- |
| **Self-Hosted Compiler (`verbose`)** | `src/` (`verbose.vb`, `token.vb`, `lexer.vb`, `ast.vb`, `printer.vb`, `parser.vb`, `type_analyzer.vb`, `codegen.vb`) | 8-module compiler written in VerBose-Lang using a single 32 MiB deterministic `Arena`. Features full semantic Type Analyzer (`--ta`/`--no-ta`), AST/IR inspection (`--ast`/`--ir`), direct compile-and-run CLI (`./verbose file.vb`), and passes Stage 1 → Stage 2 → Stage 3 byte-for-byte fixpoint (`make build`) plus **105/105** tests (`make test`). |
| **Stage 0 Bootstrap Compiler** | `stage0/` (`stage0/bin/`, `stage0/lib/`, `stage0/verbose`) | Original OCaml/ocamllex/Menhir compiler (`stage0/verbose`) retained for bootstrapping and cross-verification (**92/92** regression tests via `make test-stage0`). |
| **Tooling & CI** | `makefile`, `.github/workflows/` (`ci.yml`, `release.yml`) | `make install`/`uninstall`/`dist` packaging and GitHub Actions CI + automated Linux x86-64 binary releases. |
| **Examples & Bindings** | `examples/`, `prototype.vb`, `raylib/` | 14 standalone language examples plus the Raylib 5.x C-interop bouncing-ball demo (`examples/raylib/`). |
| **Test Suites** | `test/` | Automated CLI, example, and regression runners for both `./verbose` (`make test`) and `stage0/verbose` (`make test-stage0`). |

---

## Compiler Pipeline

### 1. Lexer & Multi-File Module Loader
- **Self-hosted (`src/lexer.vb`)**: Single-pool `g_srcPool` byte arena, hex & decimal integers, `Single`/`Double` floating-point literals, escape-aware strings, inline/EOF comments (`'`), `Include "path.vb"` splicing with `-I` search directories, canonical `.`/`..` path normalization, circular-include detection, include-once diamond deduplication, and per-file `modId` tagging.
- **Stage 0 (`stage0/lib/lexer.mll`, `loader.ml`)**: `ocamllex` + Menhir parser (3 S/R conflicts via unified `postfix_expr`) + `-I` search path resolution, circular-include detection, and module-level `Private` alpha-renaming.

### 2. Parser, AST Printer & Semantic Type Analyzer
- **Self-hosted (`src/ast.vb`, `src/parser.vb`, `src/printer.vb`, `src/type_analyzer.vb`)**: Arena-allocated flat AST node & child-index pools (`g_nodes()`, `g_kids()`), full `--ast` pretty-printer, and automatic semantic Type Analyzer (`--ta` verbose, `--no-ta` opt-out) validating variable/function/call disambiguation, numeric/pointer assignability, `ByRef` lvalue/mode rules, `Static` constant initializers, `With ( ... )` field checks, `Arena`/`Heap`/`Array` semantics, and cross-module `Private` encapsulation.
- **Stage 0 (`stage0/lib/ast.ml`, `type_analyzer.ml`)**: Reference OCaml semantic Type Analyzer & AST printer with full parity against the self-hosted implementation.

### 3. LLVM IR Codegen & CLI Driver
- **Self-hosted (`src/codegen.vb`, `src/verbose.vb`) & Stage 0 (`stage0/lib/codegen.ml`)**: Emits raw LLVM IR text with opaque pointers (`ptr`) and named SSA registers (`%rN`), automatically compiles to native binaries via `clang -O2`, and executes directly by default (`./verbose file.vb`).
- Supports `--ast`, `--ir`, `--ta`, `--no-ta`, `--emit-only`, `--no-run`, `-o`, `-I`, `-l`, `-L`, `-W`, extra `.c/.o/.a` inputs, `-h`/`--help`, `--version`, plus legacy 2-arg `verbose in.vb out.ll`.
- Handles deterministic zero-initialized `Dim`, ABI-aligned struct/array layouts & `memcpy`, struct return values, SysV x86-64 C ABI ByVal struct lowering for `Declare` (`Color` → `i32`, `Vector2` → `<2 x float>`, `Rectangle` → `<2 x float>, <2 x float>`, `>16B` → `ptr byval(%struct.X)`), scalar coercions & variadic `float → double` promotion, IEEE-754 hex `Single` global constants, module-scoped `Private` symbol mangling (`__mod<N>_<name>`), function-local `Static` globals (`@__vb_static_N`), and lazily emitted deterministic `Arena` runtime helpers.

---

## Implemented Features

- [x] **Primitive & Numeric Types**: `Byte`, `Short`, `Integer`, `Long`, `Single`, `Double`, `Boolean`, `Pointer`, `Nothing`, hex literals (`0x...`), unary negation (`-x`), contextual literal typing
- [x] **Operators**: Arithmetic (`+`, `-`, `*`, `/`, `Mod`), Comparisons (`=`, `<>`, `<`, `>`, `<=`, `>=`), Boolean & Bitwise (`And`, `Or`, `Xor`, `Not`, `Shl`, `Shr`)
- [x] **Control Flow**: `If / Then / Else / End If`, `While ... Do / End While`, `For ... To / End For`, multi-value `Select Case ... Case Else / End Select` with early `Return`
- [x] **Functions & Procedures**: `Function` / `Sub`, forward registration, `ByVal` / `ByRef` parameters, explicit call-site `ByRef` (`foo(ByRef x)`), struct return values
- [x] **Composite Types**: `Structure` (recursive ABI-aligned sizing, nested structs, deep `.` field access/assign through `ByRef`), `Enum` (lowered to `i32`), `With ( field = expr, ... )` named initializers, positional struct constructors
- [x] **Authentic VB Arrays**: Fixed arrays `Dim arr(N) As T`, unsized slices `arr() As T`, array literals `{1, 2, 3}`, `arr(i)` indexing, `Pointer` byte indexing, `ByVal`/`ByRef` arrays, `Len()`
- [x] **Static & Heap Memory**: Module-level & function-local `Static` globals, `SizeOf(Type)`, `HeapAlloc`, `HeapRealloc`, `HeapAllocBytes`, `HeapFree`
- [x] **Deterministic Arena Allocator**: `Arena` type, `ArenaCreate`, `ArenaFromBuffer`, `ArenaAlloc`, `ArenaAllocBytes`, `ArenaReset`, `ArenaMark`, `ArenaRewind`, `ArenaUsed`, `ArenaRemaining`, `ArenaCapacity`, `ArenaDestroy`, `IsNull`
- [x] **Multi-File Modules & Encapsulation**: `Include "path.vb"`, `-I` search paths, `.`/`..` path canonicalization, circular-include detection, diamond include-once deduplication, `Public`/`Private`/`Default` visibility & module-scoped `Private` isolation
- [x] **Native C Interop (FFI)**: `Declare Function/Sub`, variadic `...`, `ByRef` out-pointers, SysV x86-64 ByVal struct lowering, full string escapes (`\n`, `\r`, `\t`, `\"`, `\\`, `\0`), Raylib 5.x demo (`examples/raylib/`)
- [x] **Self-Hosted Semantic Type Analyzer & Full CLI (`src/`)**: Full Type Analyzer (`src/type_analyzer.vb`), AST pretty-printer (`src/printer.vb`), direct compile-and-execute (`./verbose file.vb`), and all driver flags (`--ast`, `--ir`, `--ta`, `--no-ta`, `--emit-only`, `--no-run`, `-o`, `-I`, `-l`, `-L`, `-W`, extra `.c/.o/.a`)
- [x] **Full Self-Hosting (`src/verbose.vb`)**: Stage 1 → Stage 2 → Stage 3 byte-for-byte fixpoint verified (**1.47 MB** identical IR) + **105/105** passing tests across CLI, examples, and all 8 regression suites
- [x] **Tooling & CI**: `make install` / `uninstall` / `dist` packaging, GitHub Actions CI (`.github/workflows/ci.yml`), and automated GitHub Release publishing (`.github/workflows/release.yml`)

---

## Verification Status

| Suite / Command | Result | Scope |
| --- | --- | --- |
| `make build` | **8/8 PASS** + Stage 3 fixpoint | `stage0/verbose` → `verbose1` → `./verbose` → `verbose2.ll == verbose3.ll` byte-for-byte identical |
| `make test` | **105/105 PASS** | `cli` 7/7, `examples` 14/14, `syntax` 10/10, `byref` 16/16, `arrays` 12/12, `static_heap` 11/11, `arena` 11/11, `type_analyzer` 12/12, `modules` 8/8, `c_interop` 4/4 |
| `make test-stage0` | **92/92 PASS** | All 9 Stage 0 suites including negative error & `--ta` semantic checks |

---

## Roadmap / TODO

- [x] **Self-Hosted Semantic Type Checker & CLI**: Ported Stage 0's Type Analyzer diagnostics (`src/type_analyzer.vb`), AST printer (`src/printer.vb`), and full CLI driver (`src/verbose.vb`) with direct compile-and-execute by default
- [x] **Tooling & CI**: Added `make install`/`uninstall`/`dist` packaging and GitHub Actions CI + Release workflows (`.github/workflows/ci.yml`, `.github/workflows/release.yml`)
- [ ] **Compress Languge Syntax**: stack old `stage0/` into `_stage0/` as **unused**, then allow common declaration like Dim, Public, Private, Static ...etc to group same type variables to reduce code/word length. Also put all messages logs as referenced constants from a common constant.vb file to avoid re-reading it when no demand to modify. Add `string concat` feature to **verbose** so in `codegen.vb` we don't need to repeat a function call just to fprintf things all the time. 
- [ ] **Cross-Compilation & Target Triples**: Emit explicit LLVM target triples & data layouts for Linux x86-64, macOS arm64, and bare-metal 32-bit ARM-M (incl. `Volatile` / fixed memory regions)
- [ ] **Standard Library Modules**: Build `std/` modules (`strings`, `math`, `collections`, `io`) using `Include` + `Arena`
- [ ] **Language Extensions**: Structured concurrency (`Async`/`Await`) and low-level address/label primitives
