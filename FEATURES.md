# VerBose-Lang — Implemented Features

VerBose-Lang is a brace-free, VB6/VB.NET-inspired systems programming language compiled directly to LLVM IR without a garbage collector.

## Language & Runtime Features

- **Types & Expressions**: `Byte`, `Short`, `Integer`, `Long`, `Single`, `Double`, `Boolean`, `Pointer`, `Nothing`, hex literals (`0x...`), unary negation (`-x`), contextual literal typing, arithmetic/comparison/logical/bitwise operators (`+`, `-`, `*`, `/`, `Mod`, `=`, `<>`, `<`, `>`, `<=`, `>=`, `And`, `Or`, `Xor`, `Not`, `Shl`, `Shr`), and native string concatenation (`&` and `+` on `Pointer`/`Byte` arrays with compile-time folding for string literals, multiline `&` continuations, and lazy runtime `@__vb_str_concat`).
- **Grouped Declarations**: Same-type and mixed-type comma-separated declarations across `Dim`, `Static`, `Public Static`, `Private Static`, `Structure` fields, and `Function`/`Sub`/`Declare` parameters (e.g. `Dim a, b, c As Integer = 0`, `Dim x = 1, y = 2 As Integer`, `Sub Swap(ByRef a, b As Integer)`).
- **Control Flow**: `If / Then / Else / End If`, `While ... Do / End While`, `For ... To / End For`, multi-value `Select Case ... Case Else / End Select` with early `Return`, plus function-scoped `Label <name>` and `GoTo <name>` with compile-time label validation.
- **Structured Concurrency (`Async` / `Await` / `Task`)**:
  - `Async Function` / `Async Sub` returning first-class `Task(T)` / `Task` handles (usable in arrays, struct fields, and helper parameters) and `Await` expressions/statements.
  - Backed by deterministic zero-GC typed frames with true OS parallelism via `pthread_create`/`pthread_join` on Linux x86-64 (auto-linking `-lpthread` only when used) and macOS arm64, and synchronous deferred thunks (zero `pthread` symbols) on freestanding bare-metal `arm-m`.
- **Functions & Parameters**: `Function` / `Sub`, forward registration, `ByVal` / `ByRef` parameters, explicit call-site `ByRef` (`foo(ByRef x)`), ABI-aligned struct return values.
- **Structs & Enums**: `Structure` (recursive ABI-aligned sizing, nested structs, deep `.` field access and assignment through `ByRef`), `Enum` (lowered to `i32`), `With ( field = expr, ... )` named initializers, positional struct constructors.
- **Authentic VB `()` Arrays**: Fixed arrays `Dim a(N) As T`, unsized slices `a() As T`, brace-free parenthesized array literals `()`, `(42)`, `(1, 2, 3)` (including inferred length `Dim b() As Integer = (1, 2, 3, 4)`), `a(i)` indexing, `Pointer` byte indexing, `ByVal`/`ByRef` array parameters, and `Len()`.
- **Static, Heap & Deterministic Arena Memory**: Module-level and function-local `Static` globals, `SizeOf(Type)`, `HeapAlloc` / `HeapRealloc` / `HeapAllocBytes` / `HeapFree`, and first-class deterministic `Arena` bump allocator (`ArenaCreate`, `ArenaFromBuffer`, `ArenaAlloc`, `ArenaAllocBytes`, `ArenaReset`, `ArenaMark`, `ArenaRewind`, `ArenaUsed`, `ArenaRemaining`, `ArenaCapacity`, `ArenaDestroy`, `IsNull`).
- **Low-Level Address, Raw Pointer & MMIO Primitives**:
  - `AddressOf(expr)`: raw `Pointer` address of local/global variables, struct fields, array elements, and functions (including `Static Pointer` constant initializers for C FFI callbacks such as `qsort`).
  - `PtrRead(addr, Type)`, `PtrWrite(addr, Type, val)`, and `PtrAdd(addr, byteOffset)`: zero-overhead non-volatile typed pointer reads/writes (scalars and aggregate structs) and byte-level pointer arithmetic.
  - `Volatile Dim` and `[Public|Private] Volatile Static`: unoptimized, non-reordered LLVM volatile loads and stores propagating through struct fields and array elements.
  - Contextual `At <int-lit>` on `Static` globals (e.g. `Volatile Static GPIOA_ODR As Integer At 0x40020014`): binds variables directly to hardware MMIO addresses via `inttoptr` with no global storage.
  - `VolatileLoad(addr, Type)` and `VolatileStore(addr, Type, val)` for raw volatile typed pointer operations.
- **Modules & Encapsulation**: `Include "path.vb"`, `-I` search directories (plus `.` fallback), `.`/`..` path normalization, circular-include detection, diamond include-once deduplication, `Public` / `Private` / `Default` visibility, and module-scoped `Private` isolation.
- **Native C Interop (FFI)**: `Declare Function` / `Sub`, variadic `...`, `ByRef` out-pointers, SysV x86-64 ByVal struct ABI lowering (`Color`, `Vector2`, `Rectangle`, `>16B` `byval`), UTF-8 & C-style string escapes (`\n`, `\r`, `\t`, `\"`, `\\`, `\0`), plus a working Raylib 5.x demo in [`tests/raylib/`](tests/raylib/).
- **Standard Library (`std/`)**: Core pure-VerBose standard library designed for zero-GC, Arena-backed systems and embedded programming:
  - `std/strings.vb`: `StrLen`, `StrCmp`, `StrEq`, `StrStartsWith`, `StrEndsWith`, `StrIndexOf`, `StrFind`, `StrCopy`, `StrSubstr`, `StrConcat`, `StrTrim`, `StrToUpper`, `StrToLower`, `StrToInt`, `IntToStr`, zero-copy `StrView` (`StrViewFrom`, `StrViewEq`), and `StrBuilder` (`SbNew`, `SbAppend`, `SbAppendChar`, `SbAppendInt`, `SbFinish`).
  - `std/math.vb`: 100% pure VerBose math routines with zero `-lm` / libc math dependency: constants (`MATH_PI`, `MATH_TAU`, `MATH_E`, `MATH_HALF_PI`, `MATH_EPS`), integer utilities (`MathAbsI`, `MathMinI`, `MathMaxI`, `MathClampI`, `MathSignI`, `MathGcd`, `MathLcm`, `MathPowI`, `MathIsqrt`), 64-bit long utilities (`MathAbsL`, `MathMinL`, `MathMaxL`, `MathClampL`), and double-precision routines (`MathAbsD`, `MathMinD`, `MathMaxD`, `MathClampD`, `MathFloor`, `MathCeil`, `MathRound`, `MathSqrt`, `MathSin`/`MathCos`/`MathTan`, `MathExp`, `MathLn`, `MathPow`, `MathLerp`, `MathNearlyEq`, `MathDegToRad`, `MathRadToDeg`).
  - `std/collections.vb`: Arena-backed `IntVec`, `PtrVec`, `IntRing` (fixed-capacity FIFO ring buffer), and `IntMap` (open-addressing linear-probing integer key-value hash map).
  - `std/io.vb`: Arena-backed file and console I/O: `IoReadFile`, `IoWriteFile`, `IoWriteText`, `IoAppendText`, `IoFileExists`, `IoFileSize`, `IoRemoveFile`, `IoPrint`, `IoPrintLn`, and `IoReadLine`.
- **Built-in Testing & Assertions (`Test` / `Assert`)**: First-class `Test "name" ... End Test` blocks and `Assert <cond> [, "message"]` statements. In normal execution `Assert` acts as a runtime assertion (printing line number & message and exiting `1` on failure); in test mode (`./verbose test [file.vb | dir]`) the compiler synthesizes a native test harness that runs every `Test` block, isolates failures, prints `[PASS]`/`[FAIL]` and summaries, and auto-discovers `test_*.vb` / `*_test.vb` files in directories (default `tests/`) — eliminating any Python dependency for testing.

## Compiler, Tooling & CI

- **100% Self-Hosted Compiler (`src/`)**: 9-module compiler (`verbose.vb`, `constants.vb`, `token.vb`, `lexer.vb`, `ast.vb`, `printer.vb`, `parser.vb`, `type_analyzer.vb`, `codegen.vb`) backed by a single 32 MiB deterministic `Arena`. Compiles itself to a byte-for-byte Stage 3 self-hosting fixpoint and passes **18/18** pure-VerBose test suites (**145** `Test` blocks + **6/6** bootstrap tests) via `./verbose test` with zero OCaml or Python dependencies. The archival OCaml Stage 0 bootstrap compiler is preserved on the `stage0` git branch.
- **Cross-Compilation & Target Triples**:
  - Target presets via `--target <name>` / `--target=<name>`: `linux-x86_64` (default), `macos-arm64` (Apple Silicon), and `arm-m` (bare-metal 32-bit Cortex-M / Thumbv7EM).
  - Target-specific LLVM `target datalayout` and `target triple` emission, eliminating `clang -Woverride-module` warnings.
  - Parameterized pointer sizes (4 bytes on `arm-m`, 8 bytes on 64-bit targets) and alignment rules.
  - Freestanding mode on `arm-m` (suppresses unused host libc imports).
  - Direct object file emission via `-c` / `--emit-obj` using `clang -O2 -c`.
- **Semantic Type Analyzer & Inspection**: Automatic semantic type checking (`--ta` verbose output, `--no-ta` opt-out), AST pretty-printer (`--ast`), and LLVM IR inspection (`--ir`).
- **Direct Compile-and-Run CLI & Built-in Test Runner**: `./verbose file.vb` type-checks, emits LLVM IR, compiles with `clang -O2`, and executes directly by default; `./verbose test [file.vb | dir]` (or `--test`) builds and runs native `Test`/`Assert` suites with full flag support (`--target`, `-c`, `--emit-only`, `--no-run`, `-o`, `-I`, `-l`, `-L`, `-W`, extra `.c/.o/.a` inputs, `--help`, `--version`).
- **Packaging & CI**: `make install` / `uninstall` / `dist` packaging plus lightweight GitHub Actions CI (`.github/workflows/ci.yml` — `clang` + `curl` only) and automated Linux x86-64 GitHub Releases (`.github/workflows/release.yml`).
