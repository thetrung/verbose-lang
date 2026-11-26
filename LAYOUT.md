# Project Layout

Overview of the VerBose language codebase, standard library, and repository layout:

```text
verbose-lang/
├── .github/workflows/          # GitHub Actions CI & Release workflows (ci.yml, release.yml)
├── src/                        # Self-hosted VerBose compiler (verbose, constants, token, lexer, ast, printer, parser, generics, type_analyzer, codegen)
├── std/                        # Standard library modules (Arena-backed, zero-GC, pure VerBose)
│   ├── collections.vb          # Generic Vec(Of T), Ring(Of T) + IntVec, PtrVec, IntRing, IntMap
│   ├── io.vb                   # File I/O, buffered reading, text/binary operations (IoRead*, IoWrite*, IoPrint*)
│   ├── math.vb                 # Pure-VerBose math routines & constants (trig, exp/ln, sqrt, pow, abs, gcd)
│   ├── span.vb                 # Generic Span(Of T) + ByteSpan, IntSpan, LongSpan, SingleSpan, PtrSpan
│   └── strings.vb              # String manipulation, StrView, and StrBuilder utilities (Str*, Sb*)
├── tests/                      # Pure-VerBose Test/Assert suites, regression cases & Raylib 5.x demo (tests/raylib/)
├── packaging/arch/             # Arch Linux PKGBUILD & .SRCINFO (`make pkg-arch`)
├── install.sh                  # Cross-platform Linux/macOS one-line installer script
├── LICENSE                     # MIT License
├── verbose.ll                  # Checked-in target-neutral LLVM IR for offline cold bootstrap (`make bootstrap`)
├── makefile                    # Build, bootstrap, bootstrap-ir, pkg-arch, run, test, install, and dist targets
├── FEATURES.md                 # Full list of implemented language & compiler features
├── TEST.md                     # Built-in testing guide (verbose test, writing tests, C linking)
├── FLAGS.md                    # Compiler command-line interface flags & options reference
├── LAYOUT.md                   # Repository directory and file structure reference
├── CACHE.md                    # Concise project summary & all exposed compiler/stdlib APIs
├── STATE.md                    # Detailed technical state & verification metrics
├── TODO.md                     # Active roadmap & future extensions
├── LOGS.md                     # Completed tasks & milestone history
└── README.md                   # Project overview, quick taste, and getting started
```

## Directory Descriptions

- **`src/`**: The complete 10-module self-hosted compiler (`14,583 LOC`) written entirely in VerBose-Lang:
  - `verbose.vb`: CLI entry point, host target auto-detection, argument parsing, pipeline orchestration, test runner synthesis, and clang invocation.
  - `constants.vb`: Version metadata, global limits, token constants, target configurations (`linux-x86_64`, `linux-arm64`, `macos-arm64`, `windows-x86_64`, `arm-m`), and diagnostics.
  - `token.vb`: Token types, keywords (`Of`, `Break`, `Continue`, `Operator`, `Match`, `When`, `String`, `Volatile`, `VolatileLoad`, `VolatileStore`, `Async`, `Await`, etc.; `ByVal` removed), string-interpolation tokens, and token structure.
  - `lexer.vb`: Lexical scanner, hex/floating-point literals, `$"..."` interpolated string scanner, and cross-platform (`/` & `\`, `<drive>:`) multi-file `Include` resolution with diamond deduplication.
  - `ast.vb`: Abstract syntax tree node definitions (`NdTypeRef`, `NdBreak`, `NdContinue`, `NdOpDef`, `NdCast`, `NdMatch`, `NdWith`, `NdStrInterp`, etc.) and flattened node arena pool (`g_nodes()`, `g_kids()`).
  - `printer.vb`: AST pretty-printer for debugging and `--ast` inspection (including `(Of ...)` generic signatures & arguments).
  - `parser.vb`: Recursive descent parser for brace-free VB grammar, `(Of T, ...)` generic parameters/arguments, built-in primitive casts (`Single(x)`, `Integer(x)`, etc.), `Break`/`Continue` statements, `Operator ... End Operator` struct overloads, `Match ... With` (plus `Select Case` desugaring), `With ... End With` blocks, enforced `For ... Do` & `While ... Do`, 64-parameter/argument signatures, arrays, MMIO declarations, and expressions.
  - `generics.vb`: Deterministic pre-TA monomorphization pass instantiating generic `Structure`, `Function`, `Sub`, `Async`, and `Operator` templates (`(Of T, ...)`) into concrete zero-overhead AST definitions with collision-free mangling, structural call-site type inference, `T(x)` cast/constructor rewriting, and recursion/depth guards.
  - `type_analyzer.vb`: Semantic type checker validating types, variables, scopes, primitive cast legality, compile-time constant fixed-array OOB errors (`TaTryEvalConstInt`), strict target-aware stack-frame limits (`--max-stack`: `64 KB` hosted / `4 KB` `arm-m`, zero silent `malloc`), loop-depth `Break`/`Continue` rules, `Structure` operator overload signatures/resolution, unqualified enum fallback & ambiguity checks, `With` struct targets, `Match` patterns/guards, `ByRef` parameters, and MMIO memory constraints.
  - `codegen.vb`: Direct LLVM IR generator supporting opaque pointers, entry-block `alloca` hoisting (O(1) loop stack + LLVM `mem2reg` promotion), explicit opt-in `--bounds-check` (`icmp ult i64` + `@__vb_bounds_fail` / `llvm.trap`), multi-target datalayouts/triples, target-native C ABI lowering (SysV x86-64, AAPCS64, Microsoft x64, ARM AAPCS), C variadic promotions, scalar/pointer/bool conversions (`NdCast`), innermost loop `Break`/`Continue` stacks, type-mangled `@__vb_op_*` operator overload dispatch, string interpolation/matching helpers, zero-copy `With` blocks, and volatile access.
- **`std/`**: Core standard library modules built for zero-GC deterministic memory:
  - `collections.vb`: Generic `Vec(Of T)` and `Ring(Of T)` plus concrete `IntVec`, `PtrVec`, `IntRing`, and open-addressing `IntMap` hash tables.
  - `io.vb`: File reading/writing (`IoReadFile`, `IoWriteFile`, `IoWriteText`, `IoAppendText`), line-by-line buffered reading (`IoReadLine`), file existence/size/deletion, and console output.
  - `math.vb`: 100% pure-VerBose mathematical functions (zero `-lm` dependency) including trigonometric series, Newton-Raphson `MathSqrt`, logarithms, powers, and rounding.
  - `span.vb`: Explicit length-carried, bounds-checked generic `Span(Of T)` plus concrete `ByteSpan`, `IntSpan`, `LongSpan`, `SingleSpan`, and `PtrSpan` with zero libc / bare-metal compatibility.
  - `strings.vb`: String operations (`StrLen`, `StrCmp`, `StrConcat`, `StrSubstr`, etc.), zero-copy `StrView`, and efficient `StrBuilder`.
- **`tests/`**: Native pure-VerBose test suites and regression tests run natively via `./verbose test`:
  - `harness.vb`: Shared cross-platform test runner helpers and assertion macros.
  - `bootstrap_suite.vb`: Comprehensive compiler self-hosting feature verification suite.
  - `test_*.vb`: 30 domain-specific test suites (`test_arena`, `test_arrays`, `test_assert`, `test_bounds`, `test_break_continue`, `test_byref`, `test_c_interop`, `test_casts`, `test_cli`, `test_compress`, `test_core`, `test_ergonomics`, `test_expressions`, `test_generics`, `test_interp`, `test_lang_ext`, `test_loop_alloca`, `test_match`, `test_modules`, `test_operators`, `test_raylib`, `test_stack_limit`, `test_static_heap`, `test_std_collections`, `test_std_io`, `test_std_math`, `test_std_strings`, `test_syntax`, `test_targets`, `test_type_analyzer`).
  - `raylib/`: Raylib 5.x C FFI bindings (`raylib.vb`), 2D/3D graphical demos (`bouncing_ball.vb`, `camera3d_demo.vb`), `README.md`, and headless test shim (`raylib_headless_shim.c`).

> **Stage 0 archive**: The original OCaml bootstrap compiler (`stage0/`) has been moved off `main` into the [`stage0`](../../tree/stage0) branch.
