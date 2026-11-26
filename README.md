# Verbose Language

[![CI](https://github.com/thetrung/verbose-lang/actions/workflows/ci.yml/badge.svg)](https://github.com/thetrung/verbose-lang/actions/workflows/ci.yml)
[![Release](https://github.com/thetrung/verbose-lang/actions/workflows/release.yml/badge.svg)](https://github.com/thetrung/verbose-lang/actions/workflows/release.yml)
[![Latest Release](https://img.shields.io/github/v/release/thetrung/verbose-lang)](https://github.com/thetrung/verbose-lang/releases/latest)

A Human-Readable Language inspired by Visual Basic, compiled directly to LLVM-IR without a garbage collector, **brace-free**, and **100% self-hosted**!

---

## Overview

Verbose-Lang pairs Visual Basic's brace-free readability with C-speed, zero-GC systems programming directly on LLVM IR. It compiles natively for Linux (x86-64, arm64), macOS (arm64), Windows (x86-64), and bare-metal 32-bit ARM-M, providing deterministic memory (`Static`, `Heap`, `Arena`), zero-overhead monomorphized generics (`(Of T, ...)` on `Structure`, `Function`, `Sub`, `Async`, and `Operator`), hardware MMIO (`Volatile`, `At`), native C FFI, built-in explicit scalar/pointer conversions (`Single(x)`, `Integer(x)`, etc.), strict compile-time stack-frame & constant array-bounds checks (`--max-stack`, opt-in `--bounds-check`, `std/span.vb`), structured `Async`/`Await` concurrency, a pure-VerBose standard library (`std/`), and built-in `Test`/`Assert` suites. The compiler (`./verbose`) is 100% self-hosted in VerBose-Lang itself with zero OCaml or Python dependencies.

See **[FEATURES.md](FEATURES.md)** for the complete feature catalog.

---

## Quick Taste (All Current Features)

```vb
' Multi-file modules & pure-VerBose stdlib (explicit bounds-checked spans)
Include "std/span.vb"

' Native C FFI (supports variadic ..., ByRef out-pointers, and ByVal C structs)
Declare Function sqrt(x As Double) As Double

Public Enum Status
  Idle = 0
  Active = 1
  Done = 2
End Enum

Public Structure Vec2
  Dim x, y As Single
End Structure

' Operator overloading over Structure (binary, mixed struct/scalar, and unary)
Public Operator +(a, b As Vec2) As Vec2
  Return Vec2(a.x + b.x, a.y + b.y)
End Operator

Public Operator *(v As Vec2, s As Single) As Vec2
  Return Vec2(v.x * s, v.y * s)
End Operator

Public Operator -(v As Vec2) As Vec2
  Return Vec2(-v.x, -v.y)
End Operator

Public Structure Entity
  Dim id As Integer
  Dim state As Status
  Dim pos As Vec2
  Dim tags(3) As Integer
End Structure

' Module-level Static global (Public / Private / Default visibility)
Private Static g_ticks As Long = 0

' Function-local persistent Static + value/ByRef parameters + With + Match
Private Sub StepEntity(ByRef e As Entity, dt As Single)
  Static calls As Integer = 0
  calls = calls + 1
  g_ticks = g_ticks + 1

  With e
    .pos = .pos + Vec2(10.5, -2.25) * dt

    Match .state With
      Case Idle Then .state = Active
      Case Active When calls >= 2 Then .state = Done
      Case _ Then .state = Done
    End Match
  End With
End Sub

Private Function SumSlice(nums() As Integer, count As Integer) As Integer
  Dim total As Integer = 0
  For i = 0 To count - 1 Do
    If nums(i) < 0 Then
      Continue
    End If
    If total > 1000 Then
      Break
    End If
    total = total + nums(i)
  End For
  Return total
End Function

' Structured Concurrency (OS pthread on Linux/macOS/Windows; deferred thunk on arm-m)
Private Async Function SquareAsync(n As Integer) As Integer
  Return n * n
End Function

Public Function main() As Integer
  ' Named With ( ... ) initializer + nested struct + fixed VB () array + unqualified Enum
  Dim hero As Entity With (
    id = 42,
    state = Idle,
    pos = Vec2 With ( x = 1.5, y = 4.0 )
  )
  hero.tags(0) = 7
  StepEntity(ByRef hero, 0.5)
  StepEntity(ByRef hero, 0.5)

  ' Structured Async / Await + rvalue member access & unary struct operator
  Dim job As Task(Integer) = SquareAsync(9)
  Dim negX As Single = (-hero.pos).x

  ' Authentic brace-free VB () array literals (fixed & inferred length) + Len()
  Dim fixed(4) As Integer = (10, 20, 30, 40)
  Dim inferred() As Integer = (1, 2, 3)
  Dim mask As Integer = (1 Shl 4) Or (8 Shr 1) Xor (Not 0)
  Dim root As Double = sqrt(16.0)

  ' Deterministic Arena allocator with Mark / Rewind / Destroy
  Dim scratch As Arena = ArenaCreate(1024)
  Dim mark As Long = ArenaMark(ByRef scratch)
  Dim pts() As Vec2 = ArenaAlloc(ByRef scratch, Vec2, 2)
  pts(0) = Vec2(3.0, 4.0)
  pts(1) = hero.pos

  ' Raw pointer primitives + Volatile & MMIO primitives
  Dim cell As Integer = 10
  PtrWrite(AddressOf(cell), Integer, 99)
  Dim cellVal As Integer = PtrRead(AddressOf(cell), Integer)
  Volatile Dim ready As Integer = 1
  VolatileStore(AddressOf(ready), Integer, 2)

  Dim used As Long = ArenaUsed(ByRef scratch)
  ArenaRewind(ByRef scratch, mark)
  ArenaDestroy(ByRef scratch)
  Dim dead As Pointer = ArenaAllocBytes(ByRef scratch, 16)

  ' Explicit Heap + std/span.vb bounds-checked views + built-in primitive casts
  Dim buf() As Integer = HeapAlloc(Integer, 3)
  Dim sp As IntSpan = IntSpanFrom(buf, 3)
  Assert IntSpanSet(sp, 0, 5) And IntSpanSet(sp, 1, 15) And IntSpanSet(sp, 2, 25)
  Dim subSp As IntSpan = IntSpanSub(sp, 1, 2)
  Dim oobVal As Integer = 0
  Assert Not IntSpanGet(subSp, 5, ByRef oobVal), "Span rejects out-of-bounds cleanly"
  Dim heapSum As Integer = SumSlice(buf, 3)
  Dim avg As Single = Single(heapSum) / Single(Len(inferred))
  Dim sq As Integer = Await job

  ' String type alias, native concat (&), and VB.NET-style string interpolation ($"...")
  Dim banner As String = "VerBose" & " v0.7.4"
  Dim summary As String = $"{banner}: hero#{hero.id} sq={sq} sub0={IntSpanAt(subSp, 0)} avg={Integer(avg)}"
  Printf("%s\n", summary)
  HeapFree(summary)
  HeapFree(buf)

  Printf("state=%d pos=(%.2f,%.2f) negX=%.2f ticks=%ld\n", hero.state, hero.pos.x, hero.pos.y, negX, g_ticks)
  Printf("fixedLen=%d heapSum=%d cell=%d sqrt=%.1f sizeof(Entity)=%d\n", Len(fixed), heapSum, cellVal, root, SizeOf(Entity))
  Printf("arenaUsed=%ld afterDestroyNull=%d mask=%d ready=%d\n", used, IsNull(dead), mask, VolatileLoad(AddressOf(ready), Integer))
  Return 0
End Function
```

---

## Project Layout

See **[LAYOUT.md](LAYOUT.md)** for the complete repository structure and directory descriptions.

---

## Usage

**Prerequisites**: `clang` (LLVM 19+ recommended). No prebuilt binary, network access, OCaml, or Python required — the repository includes a target-neutral `verbose.ll` for offline bootstrapping via `make bootstrap`! Prebuilt native binaries for **Linux x86-64**, **Linux arm64**, **macOS arm64**, and **Windows x86-64** are also published on [GitHub Releases](https://github.com/thetrung/verbose-lang/releases/latest).

### Quick Install

**Linux & macOS (one-line installer):**
```bash
curl -fsSL https://raw.githubusercontent.com/thetrung/verbose-lang/main/install.sh | sh
# Optional: custom prefix or source cold-bootstrap from verbose.ll
# curl -fsSL https://raw.githubusercontent.com/thetrung/verbose-lang/main/install.sh | sh -s -- --prefix /usr/local
# curl -fsSL https://raw.githubusercontent.com/thetrung/verbose-lang/main/install.sh | sh -s -- --from-source
```

**Arch Linux (`pacman` custom repo via GitHub Releases):**
Add to `/etc/pacman.conf` (available starting from releases publishing the Arch repo assets):
```ini
[verbose-lang]
SigLevel = Optional TrustAll
Server = https://github.com/thetrung/verbose-lang/releases/latest/download
```
Then install with:
```bash
sudo pacman -Sy verbose-lang
```
Or build & install locally from the repository using `makepkg`:
```bash
make pkg-arch
sudo pacman -U packaging/arch/verbose-lang-*.pkg.tar.zst
```

### Build from Source & CLI Usage

```bash
# Cold-bootstrap ./verbose directly from checked-in verbose.ll using clang only (Stage 1 -> Stage 2 -> Stage 3 fixpoint)
make bootstrap

# Build & verify the self-hosted compiler (./verbose) via Stage 1 -> Stage 2 -> Stage 3 fixpoint
# Uses existing ./verbose, `verbose` in PATH, BOOTSTRAP=/path/to/verbose, checked-in verbose.ll, or latest GitHub Release
make build

# Refresh checked-in target-neutral verbose.ll after compiler changes
make bootstrap-ir

# Compile and execute any .vb file with a main() function (default mode)
./verbose your_file.vb

# Link against C source/object files or system/static libraries (flags pass through to clang)
./verbose tests/raylib/bouncing_ball.vb tests/raylib/raylib_headless_shim.c -lm
./verbose tests/raylib/bouncing_ball.vb -lraylib -lm

# Run built-in Test/Assert blocks (required for test files without a main() function)
./verbose test tests/test_std_io.vb               # run a single test file
./verbose test                                    # run all 29 tests/test_*.vb suites
./verbose test --bounds-check tests/test_bounds.vb # opt-in runtime bounds checking for debug/test builds

# Enforce custom per-function stack frame budget (default: 64 KB hosted / 4 KB arm-m)
./verbose --max-stack 16384 your_file.vb

# Cross-compile for bare-metal ARM-M, Linux arm64, macOS arm64, or Windows x86-64 to object file
./verbose --target arm-m -c your_file.vb -o your_file.o
./verbose --target linux-arm64 -c your_file.vb -o your_file.o
./verbose --target macos-arm64 -c your_file.vb -o your_file.o
./verbose --target windows-x86_64 -c your_file.vb -o your_file.o

# Compile and run a specific target (default: make build)
make build

# Install ./verbose globally (default: /usr/local/bin/verbose) or build release artifacts
make install          # or: make install PREFIX=$HOME/.local
make dist             # builds stripped platform binary in dist/ + dist/SHA256SUMS.txt

# Run pure-VerBose test suites (zero Python!) — see TEST.md for full testing & C-linking guide
make test             # runs ./verbose test
make test-all         # Full pipeline: Stage 1-3 build fixpoint + make test
```

### Compiler CLI Flags

See **[FLAGS.md](FLAGS.md)** for the complete command-line interface flags and options reference.

---

## Features

All implemented language, memory, FFI, standard library, compiler, and tooling features are documented in **[FEATURES.md](FEATURES.md)**.

---

## Design Goals

- Bootstrap minimally and stay 100% self-hosted.
- Human-readable, brace-free, VisualBasic-inspired syntax that fits in your head.
- Case-sensitive to eliminate C/DLL `Alias` boilerplate.
- Fast as C/Rust with zero GC, native C-interop.
- Deterministic memory (`Arena`, `Static`, `Heap`, raw `Pointer`).
- Portable across LLVM targets — Linux (x86-64, arm64), macOS (arm64), Windows (x86-64), and bare-metal 32-bit ARM-M microcontrollers.

---

## Ecosystem

Official companion repositories built for VerBose-Lang `v0.7.x`:

- **[verbose-raylib](https://github.com/thetrung/verbose-raylib)** — **100% pure VerBose-Lang** wrapper for Raylib 5.x/6.x (zero custom C shims!) with native ABI structs, 2D/3D math, color palettes, orbital camera helpers, pure-VerBose headless mock, and graphical demos.
- **[verbose.nvim](https://github.com/thetrung/verbose.nvim)** — Neovim plugin with Tree-sitter grammar & queries, recursive `Include`-cached in-process LSP (completion, `gd`, hover, diagnostics), and `:VerboseInstall` latest compiler installer.
- **[verbose-pkg](https://github.com/thetrung/verbose-pkg)** — Serverless Cargo-inspired package manager (`vpm v0.2.0`), written natively in VerBose-Lang with multithreaded `Async`/`Await` parallel `install`/`build`/`test`, `vpm run` argument forwarding, and git/path/`.vpkg.tar.gz` bundles.
- **[verbose-tui](https://github.com/thetrung/verbose-tui)** — **100% pure VerBose-Lang** Ratatui-inspired zero-GC double-buffered 24-bit TrueColor TUI framework using direct POSIX `termios`/`ioctl`/`poll` FFI (zero `ncurses`, zero custom C shims) and Unicode widgets.
- **[verbose-openSKP](https://github.com/thetrung/verbose-openSKP)** — VerBose-Lang + C ABI wrapper around vendored MIT OpenSKP for loading SketchUp `.skp` scenes (meshes, instanced hierarchy, materials, layers, scenes/pages) directly into flat RAM arrays.
- **[verbose-viewSKP](https://github.com/thetrung/verbose-viewSKP)** — **100% pure VerBose-Lang** ZBrush-style multithreaded CPU Pixol + 2.5D Toon/Ink, Cook-Torrance GGX PBR, and 2-Level CPU BVH RayTracing `.skp` studio with 60 FPS GPU BBox / CPU monochrome line preview (`[F5]`), adjustable 2.5D Depth-of-Field focus blur (`[F6]`), progressive partial rendering (`[F7]`), draggable 3D lights, and object outliner (`verbose-openSKP` + `verbose-raylib` via `vpm`).

---

## Lesson Learned

Building and dogfooding all 6 ecosystem repositories (`verbose-raylib`, `verbose.nvim`, `verbose-pkg`, `verbose-tui`, `verbose-openSKP`, `verbose-viewSKP`) from scratch revealed how much VerBose-Lang's intentional simplicity — and strict zero-magic discipline — accelerates real-world systems work:

1. **Direct C ABI & Pure-VerBose System FFI**: `Structure` layout matches C verbatim (`12/12` `SizeOf(...) == sizeof(...)` checks matched Raylib 6.2 directly) and `verbose` forwards `.c`/`.a`/`-l`/`-I` flags straight to `clang`. Combined with `v0.7.1` built-in primitive casts (`Single(x)`, `Integer(x)`, `Byte(x)`, `Pointer(x)`), `AddressOf`, and `PtrRead`/`PtrWrite`/`PtrAdd`, we eliminated **every custom C shim** across `verbose-raylib`, `verbose-tui` (direct POSIX `termios`/`ioctl`/`poll`), and `verbose-viewSKP` — keeping C/C++ strictly to vendored third-party engines like OpenSKP.
2. **Explicit, Predictable Memory Beats Hidden Magic**: Fixing real production issues in `verbose-viewSKP` drove `v0.7.2`–`v0.7.4`: hoisting all `alloca`s to LLVM `entry:` blocks eliminated stack growth in long-running loops, while strict compile-time `--max-stack` errors (`64 KB` hosted / `4 KB` `arm-m`, never silently spilling `Dim` to `malloc`), compile-time constant fixed-array OOB errors, opt-in `--bounds-check`, and zero-libc `std/span.vb` keep performance and failure modes 100% transparent.
3. **`Arena` + Value `Structure`s + `With` + `Operator` Overloading**: Deterministic `Arena` allocation and value structs remove borrow-checker ceremony, header/source duplication, and GC-FFI coordination, while `v0.6.0`–`v0.7.x` ergonomics (`Match ... With`, `$"..."`, `With`, grouped `Dim`, `For ... Do`, `Break`/`Continue`, `Structure` `Operator` overloading, `Async`/`Await`) keep math-heavy 3D/raytracing and systems code compact and readable.
4. **Regular, Line-Oriented Syntax & Built-in `Test`**: Unambiguous declarations made it straightforward to build a 100%-parsing Tree-sitter grammar, a zero-dependency in-process Neovim LSP, and a serverless multithreaded package manager (`vpm`), while built-in `Test ... End Test` + sub-second native compiles give native C speed with scripting-level iteration.

---

## Roadmap

See **[TODO.md](TODO.md)** for the active roadmap and future extensions, and **[LOGS.md](LOGS.md)** for completed tasks and milestone history.
