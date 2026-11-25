# AGENTS.md — VerBose-Lang

Project memory & engineering guide for AI agents working in `verbose-lang`.
For mutable status and roadmap, check [STATE.md](STATE.md) and [TODO.md](TODO.md) first.
See also: [Wiki Tutorial](https://github.com/thetrung/verbose-lang/wiki/Tutorial), [FEATURES.md](FEATURES.md), [FLAGS.md](FLAGS.md), [LAYOUT.md](LAYOUT.md), [TEST.md](TEST.md), [LOGS.md](LOGS.md).

## Project Identity

- **VerBose-Lang** (`v0.6.1`): brace-free, case-sensitive, Visual Basic–inspired systems language compiled directly to LLVM IR via `clang -O2` (zero GC, native C ABI, **100% self-hosted**).
- **Zero external toolchain deps**: `clang` + `curl` only (no OCaml, no Python). The archival OCaml Stage 0 compiler lives on the `stage0` git branch.
- **Targets** (`--target` / `VERBOSE_TARGET`): `linux-x86_64`, `linux-arm64`, `macos-arm64`, `windows-x86_64`, and freestanding 32-bit `arm-m` (`thumbv7em-unknown-none-eabi`).
- **Companion ecosystem repos** (sibling dirs in `../` when checked out): `verbose-raylib`, `verbose.nvim`, `verbose-pkg` (`vpm`), `verbose-tui`.

## Architecture & Compiler Internals (`src/`, 9 modules, ~10.7k LOC)

Every compilation allocates a single **32 MiB deterministic `Arena`** in `RunSingleTarget` (`src/verbose.vb`) for source pools, tokens, AST nodes, and symbol tables:

| Module | Responsibility & Key Internals |
| --- | --- |
| `src/verbose.vb` | CLI driver, host auto-detection (`VERBOSE_TARGET`, `OS=Windows_NT`, `uname -sm`), `verbose test` runner (`test_*.vb` / `*_test.vb`), `clang -O2` invocation, auto-links `-lpthread` only when `CgUsesAsync() = 1` on Linux/Windows. |
| `src/constants.vb` | Version string (`MSG_CLI_VERSION`), diagnostics, `TargetKind`, LLVM datalayouts/triples, inline IR runtime templates (`@__vb_arena_*`, `@__vb_assert_fail`, `@__vb_str_eq`, `@__vb_str_concat`, test harness). |
| `src/token.vb`, `src/lexer.vb` | `g_srcPool` (1 MiB byte pool), `g_tokens` (131,072 cap), `$"..."` interpolation scanner, cross-platform (`/` & `\`, `<drive>:`) `Include` loader (max 64 files, 32 `-I` dirs; search: current file dir → `-I` → CWD `.`; diamond include-once + circular-include detection; per-file `modId`). |
| `src/ast.vb`, `src/parser.vb`, `src/printer.vb` | Flat AST pools `g_nodes` (65,536) & `g_kids` (131,072), `--ast` pretty-printer. `Select Case` desugars into `NdMatch`; `With` blocks emit `NdWithTarget`; compile-time literal folding (`PFoldStrLit`) on `&` and `+`. |
| `src/type_analyzer.vb` | Two-pass semantic checker (1a enums/struct headers → 1b struct fields → 1c globals/funcs/externs → 2 bodies & const global inits). Enforces `Private` module isolation, unqualified enum fallback + ambiguity errors, `ByRef` lvalue rules, `Static` const initializers, `At` MMIO rules, `Label`/`GoTo`, `Async`/`Await`. |
| `src/codegen.vb` | Direct LLVM IR emitter (opaque `ptr`). Mangles `Private` symbols from included modules (`modId > 0`) as `@__mod<id>_<name>`. Aliases `popen`/`pclose` → `_popen`/`_pclose` on Windows. Target-native C ABI struct lowering (`CgClassifyCStruct`: SysV x86-64, AAPCS64 HFA/packed/indirect, Win64, ARM AAPCS), C variadic promotions (`Single→Double`, `Boolean→zext i32`, `Byte/Short→sext i32`), short-circuit boolean `And`/`Or` via phi trampolines, freestanding `arm-m` (`cg_ptrSize = 4`, suppresses unused libc/`pthread` externs, lowers `Async` to synchronous deferred thunks). |

### Standard Library (`std/`, 4 modules, ~1.7k LOC)
All pure VerBose, zero-GC, Arena-backed:
- `std/strings.vb` — `Str*`, `StrView`, `StrBuilder` (zero libc deps, bare-metal safe).
- `std/math.vb` — `MATH_*`, `Math*` integer/long/double/trig/exp/ln/pow/sqrt (**100% pure VerBose, zero `-lm`**).
- `std/collections.vb` — `IntVec`, `PtrVec`, `IntRing`, `IntMap`.
- `std/io.vb` — `IoBuf`, `IoReadFile`, `IoWriteFile`, `IoWriteText`, `IoAppendText`, `IoFileExists`, `IoFileSize`, `IoRemoveFile`, `IoPrint`, `IoPrintLn`, `IoReadLine`.

### Tests (`tests/`)
- 23 native pure-VerBose `Test`/`Assert` suites (`tests/test_*.vb`, 169 `Test` blocks) + `tests/bootstrap_suite.vb` + `tests/harness.vb` + `tests/raylib/` (Raylib 5.x bindings, demos & headless C shim).

## Build, Bootstrap & Test Commands

```bash
make bootstrap                            # Cold-bootstrap ./verbose from checked-in verbose.ll via clang + Stage 1->2->3 fixpoint
make build                                # Stage 1 -> 2 -> 3 fixpoint (diff verbose2.ll verbose3.ll) + bootstrap_suite.vb
make bootstrap-ir                         # Regenerate checked-in target-neutral verbose.ll from ./verbose
make test                                 # Run all 23 pure-VerBose suites (./verbose test)
make test-all                             # Full fixpoint build + all tests
./verbose test tests/test_<name>.vb       # Run a single suite
./verbose --ast file.vb                   # Dump parsed AST
./verbose --ta --emit-only file.vb        # Type-check & emit .ll only
./verbose --ir --emit-only file.vb        # Print generated LLVM IR
./verbose --target arm-m -c file.vb       # Cross-compile freestanding Thumb .o
```

## VerBose Language Quick Memory (Writing `.vb` Correctly)

- **Syntax**: Case-sensitive PascalCase keywords (`Dim`, `Integer`, `End If`, `True`, `False`); comments `'`; no `{}` or `[]` anywhere except `{expr}` inside `$"..."` (escape literal braces as `{{`/`}}`).
- **Types**: `Byte`/`Short`/`Integer`/`Long`, `Single`/`Double`, `Boolean`, `Pointer`/`String` (alias), `Arena`, `Task`/`Task(T)`, `Nothing`. Uninitialized locals auto zero-initialize.
- **Declarations & Params**: Grouped same-type & mixed-type `Dim`/`Static`/fields/params allowed (`Dim a, b As Integer = 0`, `Dim x = 1, y = 2 As Integer`). Pass-by-value is default (**no `ByVal` keyword**). `ByRef` is required at **both** callee and caller (and propagates across grouped params before `As`).
- **Control Flow**: `Do` is **mandatory** on `While cond Do` and `For i = a To b Do` (inclusive bounds, step `+1`, `i` auto-declared `Integer`). No `ElseIf`, `Break`, or `Continue` — use `Match With` as an `ElseIf` ladder and `Return`, flags, or `Label`/`GoTo` for loop exits.
- **Operators & Precedence**: `And`/`Or` short-circuit on `Boolean` and are bitwise on integers (`Shr` is logical right shift `lshr`). `Not`, `And`, `Or`, `Xor` bind looser than comparisons — parenthesize bitwise ops in comparisons (`(m And 4) = 4`) and compound boolean clauses. No implicit int/float mixing in binary ops.
- **Strings**: `s1 = s2` compares pointers, NOT text — use `StrEq(s1, s2)` or `Match s With` (null-safe `strcmp`). Runtime `&`/`+` concat and `$"..."` allocate via `calloc` (call `HeapFree` or use `Arena` + `std/strings.vb` in hot loops).
- **Enums & Structs**: Every `Enum` member requires explicit `= <int>` (no auto-numbering); unqualified member lookup works unless ambiguous. Structs support positional `T(a, b)`, named `T With ( a = 1 )`, and zero-copy `With lval ... End With` blocks.
- **Built-in C helpers** (no `Declare` needed): `Printf`, `Malloc`, `Calloc`, `Realloc`, `Free`, `Fopen`, `Getc`, `Putc`.

## Hard Invariants & Gotchas

1. **Bootstrap fixpoint safety**: `make build` and `make bootstrap` must pass Stage 3 byte-for-byte (`verbose2.ll == verbose3.ll`). Never dogfood new syntax inside `src/` before bootstrapping a `./verbose` binary that parses it; refresh checked-in `verbose.ll` via `make bootstrap-ir` when updating compiler syntax/releases (and update `BOOTSTRAP_CHAIN` in `makefile` if adding a cut-point).
2. **Multi-target & C ABI parity**: Any `codegen.vb` change must preserve pointer sizes (`4` on `arm-m`, `8` on 64-bit), all 4 C struct ABIs, C variadic promotions, and freestanding `arm-m` cleanliness (no unused host libc/`pthread` decls).
3. **Pure stdlib & zero-script CI**: Keep `std/math.vb` free of `-lm`, `std/strings.vb` free of libc, and builds/tests/CI free of Python/OCaml.
4. **Docs hygiene**: Update `FEATURES.md`, `FLAGS.md`, `LAYOUT.md`, `STATE.md`, `TODO.md`, `LOGS.md` (and the GitHub Wiki if language syntax/features change) when modifying compiler capabilities.
