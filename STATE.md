# VerBose-Lang — Project State

**VerBose-Lang** is a brace-free, VB6/VB.NET-inspired systems programming language compiled directly to LLVM IR (no GC, no native LLVM binding dependency, targeting Linux x86-64/arm64, macOS arm64, Windows x86-64, and bare-metal ARM-M) — now **100% self-hosted on `main`** via `./verbose`, written entirely in VerBose-Lang itself.

---

## Architecture Overview

| Component | Location | Description |
| --- | --- | --- |
| **Self-Hosted Compiler (`verbose`)** | `src/` (`verbose.vb`, `constants.vb`, `token.vb`, `lexer.vb`, `ast.vb`, `printer.vb`, `parser.vb`, `type_analyzer.vb`, `codegen.vb`) | 9-module compiler (`v0.5.1`) written in VerBose-Lang using a single 32 MiB deterministic `Arena`. Features full semantic Type Analyzer (`--ta`/`--no-ta`), AST/IR inspection (`--ast`/`--ir`), grouped declarations, native string concatenation (`&`/`+`), cross-target triples (`--target linux-x86_64`, `linux-arm64`, `macos-arm64`, `windows-x86_64`, `arm-m`), object file emission (`-c`/`--emit-obj`), MMIO volatile memory & `At <addr>`, built-in `Test`/`Assert` & `./verbose test` runner, direct compile-and-run CLI (`./verbose file.vb`), brace-free `(..)` array & `With (..)` syntax, and Stage 1 → Stage 2 → Stage 3 byte-for-byte fixpoint (`make build`). |
| **Standard Library (`std/`)** | `std/` (`collections.vb`, `io.vb`, `math.vb`, `strings.vb`) | Pure-VerBose, zero-GC, Arena-backed standard library providing dynamic vectors/ring buffers/hash maps, comprehensive file & console I/O, 100% pure math routines (zero `-lm`), and rich string/builder utilities. |
| **Stage 0 Bootstrap Archive** | `stage0` git branch (`stage0/bin/`, `stage0/lib/`, `stage0/verbose`) | Original OCaml/ocamllex/Menhir compiler updated with `(..)` array literals and archived off `main` into the `stage0` branch. `main` bootstraps directly from an existing `./verbose` binary or the latest GitHub Release (`clang` + `curl` only — no OCaml or Python). |
| **Tooling & CI** | `makefile`, `.github/workflows/` (`ci.yml`, `release.yml`), `FLAGS.md`, `LAYOUT.md` | Platform-aware `makefile` (`install`/`uninstall`/`dist`), modular layout & flags documentation, pure self-hosted GitHub Actions CI (`clang` + `curl` only), and automated multi-platform GitHub Releases (`v0.5.1`: Linux x86-64, Linux arm64, macOS arm64, Windows x86-64). |
| **Tests, Demos & Docs** | `tests/`, `tests/raylib/`, `prototype.vb`, `README.md`, `FEATURES.md`, `FLAGS.md`, `LAYOUT.md`, `STATE.md`, `TODO.md` | 19 pure-VerBose `Test`/`Assert` suites (`tests/harness.vb`, `tests/test_*.vb`, `tests/bootstrap_suite.vb`) plus the Raylib 5.x demo, headless test shim (`tests/raylib/`), and dedicated `tests/test_raylib.vb` suite, run natively via `./verbose test`. |

---

## Compiler Pipeline

### 1. Lexer & Multi-File Module Loader (`src/token.vb`, `src/lexer.vb`)
- Single-pool `g_srcPool` byte arena, hex & decimal integers, `Single`/`Double` floating-point literals, escape-aware strings, inline/EOF comments (`'`), brace-free punctuation (`{`/`}` tokens removed), `Volatile`, `VolatileLoad`, `VolatileStore` keywords, `Include "path.vb"` splicing with `-I` search directories and current working directory `.` fallback, cross-platform `/` & `\` path and `<drive>:` prefix normalization (`.`/`..` collapsing), circular-include detection, include-once diamond deduplication, and per-file `modId` tagging.

### 2. Parser, AST Printer & Semantic Type Analyzer (`src/ast.vb`, `src/parser.vb`, `src/printer.vb`, `src/type_analyzer.vb`)
- Arena-allocated flat AST node & child-index pools (`g_nodes()`, `g_kids()`), unified parenthesized syntax for grouped expressions `(e)`, `With ( ... )` struct initializers, array literals `()`, `(42)`, `(1, 2, 3)`, `Volatile Dim`, `[Public|Private] Volatile Static`, contextual `At <addr>` MMIO registers, and `VolatileLoad`/`VolatileStore` builtins.
- Full `--ast` pretty-printer and automatic semantic Type Analyzer (`--ta` verbose, `--no-ta` opt-out) validating variable/function/call disambiguation, numeric/pointer assignability, `ByRef` lvalue/mode rules, `Static` constant initializers, `With ( ... )` field checks, `Arena`/`Heap`/`Array` semantics, MMIO register constraints (literal addresses, module-level static only), and cross-module `Private` encapsulation.

### 3. LLVM IR Codegen & CLI Driver (`src/codegen.vb`, `src/verbose.vb`)
- Emits raw LLVM IR text with opaque pointers (`ptr`) and named SSA registers (`%rN`), automatically compiles to native binaries or object files via `clang -O2`, and executes directly by default (`./verbose file.vb`).
- Auto-detects host target (`VERBOSE_TARGET`, `OS=Windows_NT`, `uname -sm`) and supports cross-compilation target triples and explicit datalayouts via `--target` (`linux-x86_64`, `linux-arm64`, `macos-arm64`, `windows-x86_64`, `arm-m`), with `-Wno-override-module` on native macOS arm64.
- Supports `--target`, `-c`/`--emit-obj`, `--ast`, `--ir`, `--ta`, `--no-ta`, `--emit-only`, `--no-run`, `-o`, `-I`, `-l`, `-L`, `-W`, extra `.c/.o/.a` inputs, `-h`/`--help`, `--version`, plus legacy 2-arg `verbose in.vb out.ll`.
- Target-aware code generation: parameterized pointer sizes (4 on `arm-m`, 8 on 64-bit), target-native C ABI struct lowering (SysV x86-64 on `linux-x86_64`, AAPCS64 HFA/packed/indirect on `linux-arm64` & `macos-arm64`, Microsoft x64 scalar/indirect on `windows-x86_64`, ARM AAPCS on `arm-m`), C variadic default argument promotion (`Single→Double`, `Boolean→zext i32`, `Byte/Short→sext i32`), Windows UCRT `_popen`/`_pclose` symbol aliasing, freestanding mode on `arm-m` (suppressing unused libc externs), volatile load/store lowering, and `inttoptr` addressing for `At` globals.

---

## Implemented Features

See **[FEATURES.md](FEATURES.md)** for the full user-facing catalog. Milestone checklist:

- [x] **Primitive & Numeric Types**: `Byte`, `Short`, `Integer`, `Long`, `Single`, `Double`, `Boolean`, `Pointer`, `Nothing`, hex literals (`0x...`), unary negation (`-x`), contextual literal typing
- [x] **Operators**: Arithmetic (`+`, `-`, `*`, `/`, `Mod`), Comparisons (`=`, `<>`, `<`, `>`, `<=`, `>=`), Boolean & Bitwise (`And`, `Or`, `Xor`, `Not`, `Shl`, `Shr`)
- [x] **Control Flow**: `If / Then / Else / End If`, `While ... Do / End While`, `For ... To / End For`, multi-value `Select Case ... Case Else / End Select` with early `Return`
- [x] **Functions & Procedures**: `Function` / `Sub`, forward registration, `ByVal` / `ByRef` parameters, explicit call-site `ByRef` (`foo(ByRef x)`), struct return values
- [x] **Composite Types**: `Structure` (recursive ABI-aligned sizing, nested structs, deep `.` field access/assign through `ByRef`), `Enum` (lowered to `i32`), `With ( field = expr, ... )` named initializers, positional struct constructors
- [x] **Authentic Brace-Free VB Arrays**: Fixed arrays `Dim arr(N) As T`, unsized slices `arr() As T`, parenthesized array literals `()`, `(42)`, `(1, 2, 3)`, `arr(i)` indexing, `Pointer` byte indexing, `ByVal`/`ByRef` arrays, `Len()`
- [x] **Static & Heap Memory**: Module-level & function-local `Static` globals, `SizeOf(Type)`, `HeapAlloc`, `HeapRealloc`, `HeapAllocBytes`, `HeapFree`
- [x] **Deterministic Arena Allocator**: `Arena` type, `ArenaCreate`, `ArenaFromBuffer`, `ArenaAlloc`, `ArenaAllocBytes`, `ArenaReset`, `ArenaMark`, `ArenaRewind`, `ArenaUsed`, `ArenaRemaining`, `ArenaCapacity`, `ArenaDestroy`, `IsNull`
- [x] **Volatile & MMIO Memory**: `Volatile Dim`, `Volatile Static`, contextual `At <addr>` memory mapping, `VolatileLoad(addr, Type)`, `VolatileStore(addr, Type, val)`
- [x] **Multi-File Modules & Encapsulation**: `Include "path.vb"`, `-I` search paths, cross-platform `/` & `\` path and `<drive>:` prefix canonicalization (`.`/`..`), circular-include detection, diamond include-once deduplication, `Public`/`Private`/`Default` visibility & module-scoped `Private` isolation
- [x] **Native C Interop (FFI)**: `Declare Function/Sub`, variadic `...` with C default argument promotions (`Single→Double`, `Boolean/Byte/Short→i32`), `ByRef` out-pointers, target-native ByVal struct lowering (SysV x86-64, AAPCS64, Microsoft x64, ARM AAPCS), full string escapes (`\n`, `\r`, `\t`, `\"`, `\\`, `\0`), Raylib 5.x demo & headless shim under `tests/raylib/` plus dedicated `tests/test_raylib.vb` suite
- [x] **Standard Library (`std/`)**: Core pure-VerBose standard library modules: `std/strings.vb` (`Str*`, `StrView`, `StrBuilder`), `std/math.vb` (100% pure VerBose math, zero `-lm`), `std/collections.vb` (`IntVec`, `PtrVec`, `IntRing`, `IntMap`), and `std/io.vb` (`Io*` file and console routines)
- [x] **Cross-Compilation & Multi-Platform Native Releases (`v0.5.1`)**: First-class target support via `--target` (`linux-x86_64`, `linux-arm64`, `macos-arm64`, `windows-x86_64`, `arm-m`), automatic host detection (`VERBOSE_TARGET`), object file emission via `-c`/`--emit-obj`, explicit LLVM datalayouts & triples, freestanding ARM-M mode, and native Stage 1→2→3 self-hosted binaries published for Linux x86-64, Linux arm64, macOS arm64, and Windows x86-64
- [x] **Language Extensions (`Async`/`Await` & Low-Level Primitives)**: `Async Function`/`Sub`, `Task(T)`/`Task`, `Await` (`pthread` parallelism on Linux x86-64/arm64, macOS arm64, and Windows x86-64 via MSYS2 winpthreads; zero-dep deferred thunks on freestanding `arm-m`), `AddressOf` (variables, fields, elements, functions & C callbacks), `PtrRead`/`PtrWrite`/`PtrAdd`, and function-scoped `Label`/`GoTo`
- [x] **Self-Hosted Semantic Type Analyzer & Full CLI (`src/`)**: Full Type Analyzer (`src/type_analyzer.vb`), AST pretty-printer (`src/printer.vb`), direct compile-and-execute (`./verbose file.vb`), and all driver flags (`--target`, `-c`, `--ast`, `--ir`, `--ta`, `--no-ta`, `--emit-only`, `--no-run`, `-o`, `-I`, `-l`, `-L`, `-W`, extra `.c/.o/.a`)
- [x] **100% Self-Hosting & Stage 0 Retirement**: `stage0/` archived on the `stage0` branch (with `(..)` array literal parity) and removed from `main`; `main` self-bootstraps to a Stage 3 byte-for-byte fixpoint and passes pure-VerBose test suites
- [x] **Built-in `Test` / `Assert` & Zero-Python Test Suite**: Added first-class `Test "name" ... End Test` blocks, `Assert cond [, "msg"]` statements, and `./verbose test [file.vb | dir]` CLI runner with directory auto-discovery (default `tests/`); consolidated `examples/` into `tests/`, replaced all Python runners with pure-VerBose suites (`tests/harness.vb`, `tests/test_*.vb`, `tests/bootstrap_suite.vb`), and removed `python3` from CI/Release workflows
- [x] **Syntax Compression & Compiler Ergonomics**: Grouped same-type & mixed-type declarations across `Dim`, `Static`, `Public`/`Private Static`, `Structure` fields, and `Function`/`Sub`/`Declare` parameters; shared `src/constants.vb` module for compiler diagnostics & LLVM IR runtime templates; native string concatenation (`&` and `+` on `Pointer`/`Byte` arrays) with compile-time folding, multiline `&` continuations, and lazy runtime `@__vb_str_concat`; compiler & tests refactored to dogfood the compressed syntax
- [x] **Tooling, Docs & CI**: Platform-aware `make install` / `uninstall` / `dist` packaging, `LAYOUT.md` & `FLAGS.md` references extracted from `README.md`, `FEATURES.md` & `TODO.md`, lightweight self-hosted GitHub Actions CI (`.github/workflows/ci.yml` — `clang` + `curl` only), and automated multi-platform GitHub Release publishing (`.github/workflows/release.yml`)

---

## Verification Status

| Suite / Command | Result | Scope |
| --- | --- | --- |
| `make build` | **6/6 PASS** + Stage 3 fixpoint | Bootstrap (`./verbose` or latest release) → `verbose1` → `./verbose` → `verbose2.ll == verbose3.ll` byte-for-byte identical + `./verbose test tests/bootstrap_suite.vb` |
| `make test` | **19/19 suites (151/151 Test blocks) PASS** | `./verbose test`: `test_arena` 11/11, `test_arrays` 12/12, `test_assert` 5/5, `test_byref` 16/16, `test_c_interop` 4/4, `test_cli` 3/3, `test_compress` 6/6, `test_core` 7/7, `test_lang_ext` 6/6, `test_modules` 8/8, `test_raylib` 4/4, `test_static_heap` 11/11, `test_std_collections` 6/6, `test_std_io` 7/7, `test_std_math` 9/9, `test_std_strings` 6/6, `test_syntax` 10/10, `test_targets` 8/8, `test_type_analyzer` 12/12 |
| Multi-Platform Release (`v0.5.1`) | **5/5 CI jobs PASS** | Native Stage 1→2→3 fixpoint + tests + published binaries on `ubuntu-latest` (x86-64), `ubuntu-24.04-arm` (arm64), `macos-15` (arm64), and `windows-latest` (MSYS2 CLANG64 x86-64) with unified `SHA256SUMS.txt` |
| `stage0` branch (`bc91476`) | **92/92 PASS** | Archived OCaml Stage 0 compiler verified with `(..)` array literal parity prior to retiring `stage0/` from `main` |

---

## Roadmap / TODO

See **[TODO.md](TODO.md)** for the active roadmap and completed milestones.
