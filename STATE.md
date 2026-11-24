# VerBose-Lang — Project State

**VerBose-Lang** is a brace-free, VB6/VB.NET-inspired systems programming language compiled directly to LLVM IR (no GC, no native LLVM binding dependency, targeting Linux x86-64, macOS arm64, and bare-metal ARM-M) — now **100% self-hosted on `main`** via `./verbose`, written entirely in VerBose-Lang itself.

---

## Architecture Overview

| Component | Location | Description |
| --- | --- | --- |
| **Self-Hosted Compiler (`verbose`)** | `src/` (`verbose.vb`, `token.vb`, `lexer.vb`, `ast.vb`, `printer.vb`, `parser.vb`, `type_analyzer.vb`, `codegen.vb`) | 8-module compiler written in VerBose-Lang using a single 32 MiB deterministic `Arena`. Features full semantic Type Analyzer (`--ta`/`--no-ta`), AST/IR inspection (`--ast`/`--ir`), direct compile-and-run CLI (`./verbose file.vb`), brace-free `(..)` array & `With (..)` syntax, Stage 1 → Stage 2 → Stage 3 byte-for-byte fixpoint (`make build`), and **105/105** tests (`make test`). |
| **Stage 0 Bootstrap Archive** | `stage0` git branch (`stage0/bin/`, `stage0/lib/`, `stage0/verbose`) | Original OCaml/ocamllex/Menhir compiler updated with `(..)` array literals and archived off `main` into the `stage0` branch. `main` bootstraps directly from an existing `./verbose` binary or the latest GitHub Release (`clang` + `python3` only). |
| **Tooling & CI** | `makefile`, `.github/workflows/` (`ci.yml`, `release.yml`) | `make install`/`uninstall`/`dist` packaging and pure self-hosted GitHub Actions CI + automated Linux x86-64 binary releases (no OCaml toolchain required). |
| **Examples & Bindings** | `examples/`, `examples/raylib/`, `prototype.vb` | 14 standalone language examples plus the consolidated Raylib 5.x demo, headless test shim, and legacy 3D camera draft (`examples/raylib/`). |
| **Documentation & Tests** | `README.md`, `Features.md`, `STATE.md`, `test/` | Full feature catalog extracted to `Features.md`; automated CLI, example, bootstrap fixpoint, and regression test runners under `test/`. |

---

## Compiler Pipeline

### 1. Lexer & Multi-File Module Loader (`src/token.vb`, `src/lexer.vb`)
- Single-pool `g_srcPool` byte arena, hex & decimal integers, `Single`/`Double` floating-point literals, escape-aware strings, inline/EOF comments (`'`), brace-free punctuation (`{`/`}` tokens removed), `Include "path.vb"` splicing with `-I` search directories, canonical `.`/`..` path normalization, circular-include detection, include-once diamond deduplication, and per-file `modId` tagging.

### 2. Parser, AST Printer & Semantic Type Analyzer (`src/ast.vb`, `src/parser.vb`, `src/printer.vb`, `src/type_analyzer.vb`)
- Arena-allocated flat AST node & child-index pools (`g_nodes()`, `g_kids()`), unified parenthesized syntax for grouped expressions `(e)`, `With ( ... )` struct initializers, and array literals `()`, `(42)`, `(1, 2, 3)` (supporting fixed, partial, single-element, multiline, and length-inferred array declarations).
- Full `--ast` pretty-printer and automatic semantic Type Analyzer (`--ta` verbose, `--no-ta` opt-out) validating variable/function/call disambiguation, numeric/pointer assignability, `ByRef` lvalue/mode rules, `Static` constant initializers, `With ( ... )` field checks, `Arena`/`Heap`/`Array` semantics, and cross-module `Private` encapsulation.

### 3. LLVM IR Codegen & CLI Driver (`src/codegen.vb`, `src/verbose.vb`)
- Emits raw LLVM IR text with opaque pointers (`ptr`) and named SSA registers (`%rN`), automatically compiles to native binaries via `clang -O2`, and executes directly by default (`./verbose file.vb`).
- Supports `--ast`, `--ir`, `--ta`, `--no-ta`, `--emit-only`, `--no-run`, `-o`, `-I`, `-l`, `-L`, `-W`, extra `.c/.o/.a` inputs, `-h`/`--help`, `--version`, plus legacy 2-arg `verbose in.vb out.ll`.
- Handles deterministic zero-initialized `Dim`, ABI-aligned struct/array layouts & `memcpy`, struct return values, SysV x86-64 C ABI ByVal struct lowering for `Declare` (`Color` → `i32`, `Vector2` → `<2 x float>`, `Rectangle` → `<2 x float>, <2 x float>`, `>16B` → `ptr byval(%struct.X)`), scalar coercions & variadic `float → double` promotion, IEEE-754 hex `Single` global constants, module-scoped `Private` symbol mangling (`__mod<N>_<name>`), function-local `Static` globals (`@__vb_static_N`), and lazily emitted deterministic `Arena` runtime helpers.

---

## Implemented Features

See **[Features.md](Features.md)** for the full user-facing catalog. Milestone checklist:

- [x] **Primitive & Numeric Types**: `Byte`, `Short`, `Integer`, `Long`, `Single`, `Double`, `Boolean`, `Pointer`, `Nothing`, hex literals (`0x...`), unary negation (`-x`), contextual literal typing
- [x] **Operators**: Arithmetic (`+`, `-`, `*`, `/`, `Mod`), Comparisons (`=`, `<>`, `<`, `>`, `<=`, `>=`), Boolean & Bitwise (`And`, `Or`, `Xor`, `Not`, `Shl`, `Shr`)
- [x] **Control Flow**: `If / Then / Else / End If`, `While ... Do / End While`, `For ... To / End For`, multi-value `Select Case ... Case Else / End Select` with early `Return`
- [x] **Functions & Procedures**: `Function` / `Sub`, forward registration, `ByVal` / `ByRef` parameters, explicit call-site `ByRef` (`foo(ByRef x)`), struct return values
- [x] **Composite Types**: `Structure` (recursive ABI-aligned sizing, nested structs, deep `.` field access/assign through `ByRef`), `Enum` (lowered to `i32`), `With ( field = expr, ... )` named initializers, positional struct constructors
- [x] **Authentic Brace-Free VB Arrays**: Fixed arrays `Dim arr(N) As T`, unsized slices `arr() As T`, parenthesized array literals `()`, `(42)`, `(1, 2, 3)`, `arr(i)` indexing, `Pointer` byte indexing, `ByVal`/`ByRef` arrays, `Len()`
- [x] **Static & Heap Memory**: Module-level & function-local `Static` globals, `SizeOf(Type)`, `HeapAlloc`, `HeapRealloc`, `HeapAllocBytes`, `HeapFree`
- [x] **Deterministic Arena Allocator**: `Arena` type, `ArenaCreate`, `ArenaFromBuffer`, `ArenaAlloc`, `ArenaAllocBytes`, `ArenaReset`, `ArenaMark`, `ArenaRewind`, `ArenaUsed`, `ArenaRemaining`, `ArenaCapacity`, `ArenaDestroy`, `IsNull`
- [x] **Multi-File Modules & Encapsulation**: `Include "path.vb"`, `-I` search paths, `.`/`..` path canonicalization, circular-include detection, diamond include-once deduplication, `Public`/`Private`/`Default` visibility & module-scoped `Private` isolation
- [x] **Native C Interop (FFI)**: `Declare Function/Sub`, variadic `...`, `ByRef` out-pointers, SysV x86-64 ByVal struct lowering, full string escapes (`\n`, `\r`, `\t`, `\"`, `\\`, `\0`), Raylib 5.x demo consolidated under `examples/raylib/`
- [x] **Self-Hosted Semantic Type Analyzer & Full CLI (`src/`)**: Full Type Analyzer (`src/type_analyzer.vb`), AST pretty-printer (`src/printer.vb`), direct compile-and-execute (`./verbose file.vb`), and all driver flags (`--ast`, `--ir`, `--ta`, `--no-ta`, `--emit-only`, `--no-run`, `-o`, `-I`, `-l`, `-L`, `-W`, extra `.c/.o/.a`)
- [x] **100% Self-Hosting & Stage 0 Retirement**: `stage0/` archived on the `stage0` branch (with `(..)` array literal parity) and removed from `main`; `main` self-bootstraps to a Stage 3 byte-for-byte fixpoint and passes **105/105** tests
- [x] **Tooling, Docs & CI**: `make install` / `uninstall` / `dist` packaging, `Features.md` extracted from `README.md`, lightweight self-hosted GitHub Actions CI (`.github/workflows/ci.yml`), and automated GitHub Release publishing (`.github/workflows/release.yml`)

---

## Verification Status

| Suite / Command | Result | Scope |
| --- | --- | --- |
| `make build` | **8/8 PASS** + Stage 3 fixpoint | Bootstrap (`./verbose` or latest release) → `verbose1` → `./verbose` → `verbose2.ll == verbose3.ll` byte-for-byte identical |
| `make test` | **105/105 PASS** | `cli` 7/7, `examples` 14/14, `syntax` 10/10, `byref` 16/16, `arrays` 12/12, `static_heap` 11/11, `arena` 11/11, `type_analyzer` 12/12, `modules` 8/8, `c_interop` 4/4 |
| `make demo` | **13/13 PASS** | All standalone examples under `examples/` compiled & executed directly via `./verbose` |
| `stage0` branch (`bc91476`) | **92/92 PASS** | Archived OCaml Stage 0 compiler verified with `(..)` array literal parity prior to retiring `stage0/` from `main` |

---

## Roadmap / TODO

- [x] **Self-Hosted Semantic Type Checker & CLI**: Ported Stage 0's Type Analyzer diagnostics (`src/type_analyzer.vb`), AST printer (`src/printer.vb`), and full CLI driver (`src/verbose.vb`) with direct compile-and-execute by default
- [x] **Tooling & CI**: Added `make install`/`uninstall`/`dist` packaging and GitHub Actions CI + Release workflows (`.github/workflows/ci.yml`, `.github/workflows/release.yml`)
- [x] **Brace-Free Array Syntax, Stage 0 Archival & Repo Cleanup**: Replaced `{..}` array literals with `(..)` in both Stage 0 and self-hosted compilers, branched `stage0` and removed `stage0/` from `main`, moved `raylib/` into `examples/raylib/`, and extracted `Features.md`
- [ ] **Compress Language Syntax & Compiler Ergonomics**: Allow common declarations (`Dim`, `Public`, `Private`, `Static`, etc.) to group same-type variables; move compiler diagnostic/message logs into a shared `constants.vb` module; add native string concatenation so `codegen.vb` avoids repetitive `fprintf` calls
- [ ] **Cross-Compilation & Target Triples**: Emit explicit LLVM target triples & data layouts for Linux x86-64, macOS arm64, and bare-metal 32-bit ARM-M (incl. `Volatile` / fixed memory regions)
- [ ] **Standard Library Modules**: Build `std/` modules (`strings`, `math`, `collections`, `io`) using `Include` + `Arena`
- [ ] **Language Extensions**: Structured concurrency (`Async`/`Await`) and low-level address/label primitives
