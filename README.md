# VerBose Language

Just Another Human-Readable Systems Programming Language — inspired by Visual Basic 6.0 / VB.NET, compiled directly to LLVM IR without a garbage collector, and **fully self-hosted**!

---

## Overview

VerBose-Lang combines classic Visual Basic readability with deterministic, C-speed systems programming on top of LLVM:
- **Zero GC**: Stack values, `Static` storage, explicit `HeapAlloc`/`HeapFree`, and a first-class deterministic `Arena` bump allocator.
- **Native C FFI**: Direct `Declare Function`/`Sub` bindings with SysV x86-64 ByVal struct ABI lowering (including Raylib 5.x).
- **Self-Hosted Compiler (`./verbose`)**: The primary compiler (`src/verbose.vb`, `src/token.vb`, `src/lexer.vb`, `src/ast.vb`, `src/printer.vb`, `src/parser.vb`, `src/type_analyzer.vb`, `src/codegen.vb`) is written in VerBose-Lang itself, includes a full semantic Type Analyzer and direct compile-and-run CLI, compiles itself to a byte-for-byte Stage 3 fixpoint, and passes **105/105** self-hosted tests & examples.

---

## Quick Taste (All Current Features)

```vb
' Multi-file modules: Include "math_utils.vb"
' Native C FFI (supports variadic ..., ByRef out-pointers, and ByVal C structs)
Declare Function sqrt(x As Double) As Double

Public Enum Status
  Idle = 0
  Active = 1
  Done = 2
End Enum

Public Structure Vec2
  Dim x As Single
  Dim y As Single
End Structure

Public Structure Entity
  Dim id As Integer
  Dim state As Status
  Dim pos As Vec2
  Dim tags(3) As Integer
End Structure

' Module-level Static global (Public / Private / Default visibility)
Private Static g_ticks As Long = 0

' Function-local persistent Static + ByVal/ByRef parameters
Private Sub StepEntity(ByRef e As Entity, ByVal dt As Single)
  Static calls As Integer = 0
  calls = calls + 1
  g_ticks = g_ticks + 1

  e.pos.x = e.pos.x + 10.5 * dt
  e.pos.y = e.pos.y - 2.25 * dt

  Select Case e.state
    Case Status.Idle
      e.state = Status.Active
    Case Status.Active
      If calls >= 2 Then
        e.state = Status.Done
      End If
    Case Else
      e.state = Status.Done
  End Select
End Sub

Private Function SumSlice(nums() As Integer, count As Integer) As Integer
  Dim total As Integer = 0
  For i = 0 To count - 1
    total = total + nums(i)
  End For
  Return total
End Function

Public Function main() As Integer
  ' Named With ( ... ) initializer + nested struct + fixed VB () array
  Dim hero As Entity With (
    id = 42,
    state = Status.Idle,
    pos = Vec2 With ( x = 1.5, y = 4.0 )
  )
  hero.tags(0) = 7
  StepEntity(ByRef hero, 0.5)
  StepEntity(ByRef hero, 0.5)

  ' Authentic VB () array literals & Len()
  Dim fixed(4) As Integer = {10, 20, 30, 40}
  Dim mask As Integer = (1 Shl 4) Or (8 Shr 1) Xor (Not 0)
  Dim root As Double = sqrt(16.0)

  ' Deterministic Arena allocator with Mark / Rewind / Destroy
  Dim scratch As Arena = ArenaCreate(1024)
  Dim mark As Long = ArenaMark(ByRef scratch)
  Dim pts() As Vec2 = ArenaAlloc(ByRef scratch, Vec2, 2)
  pts(0) = Vec2(3.0, 4.0)
  pts(1) = hero.pos

  Dim used As Long = ArenaUsed(ByRef scratch)
  ArenaRewind(ByRef scratch, mark)
  ArenaDestroy(ByRef scratch)
  Dim dead As Pointer = ArenaAllocBytes(ByRef scratch, 16)

  ' Explicit Heap allocator & SizeOf
  Dim buf() As Integer = HeapAlloc(Integer, 3)
  buf(0) = 5
  buf(1) = 15
  buf(2) = 25
  Dim heapSum As Integer = SumSlice(buf, 3)
  HeapFree(buf)

  Printf("hero#%d state=%d pos=(%.2f,%.2f) ticks=%ld\n", hero.id, hero.state, hero.pos.x, hero.pos.y, g_ticks)
  Printf("fixedLen=%d heapSum=%d sqrt=%.1f sizeof(Entity)=%d\n", Len(fixed), heapSum, root, SizeOf(Entity))
  Printf("arenaUsed=%ld afterDestroyNull=%d mask=%d\n", used, IsNull(dead), mask)
  Return 0
End Function
```

---

## Project Layout

```text
verbose-lang/
├── .github/workflows/          # GitHub Actions CI & Release workflows (ci.yml, release.yml)
├── src/                        # Self-hosted VerBose compiler (verbose, token, lexer, ast, printer, parser, type_analyzer, codegen)
├── stage0/                     # Stage 0 OCaml bootstrap compiler (bin/, lib/, verbose)
├── examples/                   # Standalone language examples + Raylib 5.x demo
├── raylib/                     # Raylib header/binding playground
├── test/                       # Automated test suites for ./verbose & stage0/verbose
├── prototype.vb                # Scratch prototype
├── makefile                    # Build, demo, test, install, and dist targets
├── STATE.md                    # Detailed technical state & verification metrics
└── README.md
```

---

## Usage

**Prerequisites**: `clang` (LLVM 19+ recommended) and `python3` for test runners (`ocaml`/`dune`/`menhir` only needed if rebuilding `stage0/verbose`).

```bash
# Build & verify the self-hosted compiler (./verbose) via Stage 1 -> Stage 2 -> Stage 3 fixpoint
make build

# Compile and execute any .vb file directly (default mode)
./verbose your_file.vb

# Build and run prototype.vb with ./verbose
make

# Install ./verbose globally (default: /usr/local/bin/verbose) or build release artifacts
make install          # or: make install PREFIX=$HOME/.local
make dist             # builds stripped dist/verbose, dist/verbose-linux-x86_64, dist/SHA256SUMS.txt

# Run all examples
make demo

# Run test suites
make test          # 105/105 self-hosted ./verbose CLI, examples, and full regression suites
make test-stage0   # 92/92 Stage 0 OCaml compiler + Type Analyzer regression suites
make test-all      # Full pipeline: Stage 1-3 build fixpoint + make test + make test-stage0
```

### Compiler CLI Flags (`./verbose`)

| Flag | Description |
| --- | --- |
| `verbose <file.vb>` | Type-check, emit `.ll`, compile with `clang -O2`, and **execute directly** |
| `--ast` | Pretty-print the parsed AST |
| `--ir` | Print the generated LLVM IR to stdout |
| `--ta` | Verbose semantic Type Analyzer output (runs automatically before codegen) |
| `--no-ta` | Skip the semantic Type Analyzer pass |
| `--emit-only` | Emit LLVM IR (`.ll`) only; skip `clang` and execution |
| `--no-run` | Compile native executable with `clang`, skip execution |
| `-o <path>` | Output executable path (or `.ll` path with `--emit-only`) |
| `-I <dir>` / `-I<dir>` | Add `Include` module search directory |
| `-l<lib>` / `-L<dir>` / `-W<flag>` | Forward linker/warning flags to `clang` (e.g. `-lm`, `-lraylib`) |
| `<file.c/.o/.a>` | Additional C/object/archive files compiled & linked alongside `.vb` |
| `-h`, `--help`, `--version` | Show CLI usage or compiler version |

---

## Implemented Features

- **Types & Expressions**: `Byte`, `Short`, `Integer`, `Long`, `Single`, `Double`, `Boolean`, `Pointer`, `Nothing`, hex literals (`0x...`), unary `-`, contextual literal typing, arithmetic/comparison/logical/bitwise operators (`And`, `Or`, `Xor`, `Not`, `Shl`, `Shr`, `Mod`)
- **Control Flow**: `If / Then / Else`, `While ... Do`, `For ... To`, multi-value `Select Case ... Case Else` with early `Return`
- **Functions & Parameters**: `Function` / `Sub`, `ByVal` / `ByRef` parameters, explicit call-site `ByRef`, ABI-aligned struct returns
- **Structs & Enums**: `Structure` (nested structs, deep `.` field access/assignment), `Enum`, `With ( ... )` named initializers, positional struct constructors
- **Authentic VB `()` Arrays**: Fixed arrays `Dim a(N) As T`, unsized slices `a() As T`, array literals `{1, 2, 3}`, `a(i)` indexing, `Pointer` byte indexing, `Len()`
- **Static, Heap & Deterministic Arena Memory**: Module & function-local `Static`, `SizeOf(Type)`, `HeapAlloc` / `HeapRealloc` / `HeapAllocBytes` / `HeapFree`, and built-in `Arena` (`ArenaCreate`, `ArenaFromBuffer`, `ArenaAlloc`, `ArenaAllocBytes`, `ArenaReset`, `ArenaMark`, `ArenaRewind`, `ArenaUsed`, `ArenaRemaining`, `ArenaCapacity`, `ArenaDestroy`, `IsNull`)
- **Modules & Encapsulation**: `Include "path.vb"`, `.`/`..` path normalization, diamond include-once deduplication, `Public` / `Private` / `Default` visibility & module-scoped `Private` isolation
- **Native C Interop (FFI)**: `Declare Function` / `Sub`, variadic `...`, `ByRef` out-pointers, SysV x86-64 ByVal struct lowering, string escapes (`\n`, `\r`, `\t`, `\"`, `\\`, `\0`), plus working Raylib 5.x demo (`examples/raylib/bouncing_ball.vb`)
- **Self-Hosting, Type Analyzer & CLI**: 8-module compiler in `src/` with full semantic Type Analyzer (`--ta`/`--no-ta`), AST printer (`--ast`), IR printer (`--ir`), direct compile-and-run (`./verbose file.vb`), full clang/linker flags (`-o`, `-I`, `-l`, `-L`, `.c/.o/.a`), Stage 1 → Stage 2 → Stage 3 byte-for-byte fixpoint, and **105/105** tests
- **Tooling & CI**: `make install` / `uninstall` / `dist` packaging plus GitHub Actions CI (`.github/workflows/ci.yml`) and Release automation (`.github/workflows/release.yml`)

---

## Design Goals

- Bootstrap minimally and stay self-hosted.
- Human-readable VB6/VB.NET-inspired syntax that fits in your head (case-sensitive to eliminate C/DLL `Alias` boilerplate).
- Fast as C/Rust with zero GC, native C interop, and deterministic memory (`Arena`, `Static`, `Heap`, raw `Pointer`).
- Portable across LLVM targets — Linux x86-64, macOS arm64, and bare-metal 32-bit ARM-M microcontrollers.

---

## Roadmap / TODO

- [x] **Self-Hosted Type Analyzer & CLI**: Ported Stage 0's semantic Type Analyzer (`--ta`/`--no-ta`), AST printer (`--ast`), IR printer (`--ir`), and full driver flags (`-o`, `-I`, `-l`, `-L`, `--emit-only`, `--no-run`, direct compile & execute) into `src/`
- [x] **Tooling & CI**: `make install`/`uninstall`/`dist` packaging, GitHub Actions CI (`ci.yml`), and automated Linux x86-64 binary releases (`release.yml`)
- [ ] **Target Triples & Cross-Compilation**: Emit explicit LLVM target triples and data layouts for Linux x86-64, macOS arm64, and bare-metal ARM-M (`Volatile`, fixed/deterministic memory regions)
- [ ] **Standard Library (`std/`)**: Core modules (`strings`, `math`, `collections`, `io`) built with `Include` + `Arena`
- [ ] **Language Extensions**: `Async`/`Await` and low-level label/address/raw memory primitives
