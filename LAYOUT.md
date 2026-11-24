# Project Layout

Overview of the VerBose language codebase, standard library, and repository layout:

```text
verbose-lang/
├── .github/workflows/          # GitHub Actions CI & Release workflows (ci.yml, release.yml)
├── src/                        # Self-hosted VerBose compiler (verbose, constants, token, lexer, ast, printer, parser, type_analyzer, codegen)
├── std/                        # Standard library modules (Arena-backed, zero-GC, pure VerBose)
│   ├── collections.vb          # Vector, ring buffer & hash map collections (IntVec, PtrVec, IntRing, IntMap)
│   ├── io.vb                   # File I/O, buffered reading, text/binary operations (IoRead*, IoWrite*, IoPrint*)
│   ├── math.vb                 # Pure-VerBose math routines & constants (trig, exp/ln, sqrt, pow, abs, gcd)
│   └── strings.vb              # String manipulation, StrView, and StrBuilder utilities (Str*, Sb*)
├── tests/                      # Pure-VerBose Test/Assert suites, regression cases & Raylib 5.x demo (tests/raylib/)
├── prototype.vb                # Scratch prototype
├── makefile                    # Build, run, test, install, and dist targets
├── FEATURES.md                 # Full list of implemented language & compiler features
├── TEST.md                     # Built-in testing guide (verbose test, writing tests, C linking)
├── FLAGS.md                    # Compiler command-line interface flags & options reference
├── LAYOUT.md                   # Repository directory and file structure reference
├── STATE.md                    # Detailed technical state & verification metrics
├── TODO.md                     # Active roadmap & future extensions
├── LOGS.md                     # Completed tasks & milestone history
└── README.md                   # Project overview, quick taste, and getting started
```

## Directory Descriptions

- **`src/`**: The complete 9-module self-hosted compiler (`v0.5.1`) written entirely in VerBose-Lang:
  - `verbose.vb`: CLI entry point, host target auto-detection, argument parsing, pipeline orchestration, test runner synthesis, and clang invocation.
  - `constants.vb`: Version metadata, global limits, token constants, target configurations (`linux-x86_64`, `linux-arm64`, `macos-arm64`, `windows-x86_64`, `arm-m`), and diagnostics.
  - `token.vb`: Token types, keywords (`Volatile`, `VolatileLoad`, `VolatileStore`, `Async`, `Await`, etc.), and token structure.
  - `lexer.vb`: Lexical scanner, hex/floating-point literals, and cross-platform (`/` & `\`, `<drive>:`) multi-file `Include` resolution with diamond deduplication.
  - `ast.vb`: Abstract syntax tree node definitions and flattened node arena pool (`g_nodes()`, `g_kids()`).
  - `printer.vb`: AST pretty-printer for debugging and `--ast` inspection.
  - `parser.vb`: Recursive descent parser for brace-free VB grammar, arrays, MMIO declarations, and expressions.
  - `type_analyzer.vb`: Semantic type checker validating types, variables, scopes, ByRef parameters, and MMIO memory constraints.
  - `codegen.vb`: Direct LLVM IR generator supporting opaque pointers, multi-target datalayouts/triples, target-native C ABI lowering (SysV x86-64, AAPCS64, Microsoft x64, ARM AAPCS), C variadic promotions, and volatile access.
- **`std/`**: Core standard library modules built for zero-GC deterministic memory:
  - `collections.vb`: Dynamic `IntVec` and `PtrVec` arrays, fixed-capacity `IntRing` FIFO ring buffers, and open-addressing `IntMap` hash tables.
  - `io.vb`: File reading/writing (`IoReadFile`, `IoWriteFile`, `IoWriteText`, `IoAppendText`), line-by-line buffered reading (`IoReadLine`), file existence/size/deletion, and console output.
  - `math.vb`: 100% pure-VerBose mathematical functions (zero `-lm` dependency) including trigonometric series, Newton-Raphson `MathSqrt`, logarithms, powers, and rounding.
  - `strings.vb`: String operations (`StrLen`, `StrCmp`, `StrConcat`, `StrSubstr`, etc.), zero-copy `StrView`, and efficient `StrBuilder`.
- **`tests/`**: Native pure-VerBose test suites and regression tests run natively via `./verbose test`:
  - `harness.vb`: Shared cross-platform test runner helpers and assertion macros.
  - `bootstrap_suite.vb`: Comprehensive compiler self-hosting feature verification suite.
  - `test_*.vb`: 19 domain-specific test suites (`test_arena`, `test_arrays`, `test_assert`, `test_byref`, `test_c_interop`, `test_cli`, `test_compress`, `test_core`, `test_lang_ext`, `test_modules`, `test_raylib`, `test_static_heap`, `test_std_collections`, `test_std_io`, `test_std_math`, `test_std_strings`, `test_syntax`, `test_targets`, `test_type_analyzer`).
  - `raylib/`: Raylib 5.x C FFI bindings (`raylib.vb`), graphical demo (`bouncing_ball.vb`), and headless test shim (`raylib_headless_shim.c`).

> **Stage 0 archive**: The original OCaml bootstrap compiler (`stage0/`) has been moved off `main` into the [`stage0`](../../tree/stage0) branch.
