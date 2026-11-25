# Verbose Language

[![CI](https://github.com/thetrung/verbose-lang/actions/workflows/ci.yml/badge.svg)](https://github.com/thetrung/verbose-lang/actions/workflows/ci.yml)
[![Release](https://github.com/thetrung/verbose-lang/actions/workflows/release.yml/badge.svg)](https://github.com/thetrung/verbose-lang/actions/workflows/release.yml)
[![Latest Release](https://img.shields.io/github/v/release/thetrung/verbose-lang)](https://github.com/thetrung/verbose-lang/releases/latest)

A Human-Readable Language inspired by Visual Basic, compiled directly to LLVM-IR without a garbage collector, **brace-free**, and **100% self-hosted**!

---

## Overview

Verbose-Lang pairs Visual Basic's brace-free readability with C-speed, zero-GC systems programming directly on LLVM IR. It compiles natively for Linux (x86-64, arm64), macOS (arm64), Windows (x86-64), and bare-metal 32-bit ARM-M, providing deterministic memory (`Static`, `Heap`, `Arena`), hardware MMIO (`Volatile`, `At`), native C FFI, structured `Async`/`Await` concurrency, a pure-VerBose standard library (`std/`), and built-in `Test`/`Assert` suites. The compiler (`./verbose`) is 100% self-hosted in VerBose-Lang itself with zero OCaml or Python dependencies.

See **[FEATURES.md](FEATURES.md)** for the complete feature catalog.

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
  Dim x, y As Single
End Structure

' Operator overloading over Structure (supports mixed struct/scalar & unary ops)
Public Operator +(a, b As Vec2) As Vec2
  Return Vec2(a.x + b.x, a.y + b.y)
End Operator

Public Operator *(v As Vec2, s As Single) As Vec2
  Return Vec2(v.x * s, v.y * s)
End Operator

Public Structure Entity
  Dim id As Integer
  Dim state As Status
  Dim pos As Vec2
  Dim tags(3) As Integer
End Structure

' Module-level Static global (Public / Private / Default visibility)
Private Static g_ticks As Long = 0

' Function-local persistent Static + value/ByRef parameters
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

  ' Authentic brace-free VB () array literals & Len()
  Dim fixed(4) As Integer = (10, 20, 30, 40)
  Dim mask As Integer = (1 Shl 4) Or (8 Shr 1) Xor (Not 0)
  Dim root As Double = sqrt(16.0)

  ' Deterministic Arena allocator with Mark / Rewind / Destroy
  Dim scratch As Arena = ArenaCreate(1024)
  Dim mark As Long = ArenaMark(ByRef scratch)
  Dim pts() As Vec2 = ArenaAlloc(ByRef scratch, Vec2, 2)
  pts(0) = Vec2(3.0, 4.0)
  pts(1) = hero.pos

  ' Volatile variable and MMIO primitive demonstration
  Volatile Dim ready As Integer = 1
  VolatileStore(AddressOf(ready), Integer, 2)

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
./verbose test tests/test_std_io.vb   # run a single test file
./verbose test                        # run all tests/test_*.vb suites

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

Official companion repositories built for VerBose-Lang `v0.6.0+` / `v0.7.0+`:

- **[verbose-raylib](https://github.com/thetrung/verbose-raylib)** — Idiomatic VerBose-Lang + C ABI wrapper for Raylib 5.x/6.x with 2D/3D math, color palettes, orbital camera helpers, headless mock, and graphical demos.
- **[verbose.nvim](https://github.com/thetrung/verbose.nvim)** — Neovim plugin with Tree-sitter grammar & queries, recursive `Include`-cached in-process LSP (completion, `gd`, hover, diagnostics), and `:VerboseInstall` latest compiler installer.
- **[verbose-pkg](https://github.com/thetrung/verbose-pkg)** — Serverless Cargo-inspired package manager (`vpm`), written natively in VerBose-Lang, supporting git, path, and source/binary `.vpkg.tar.gz` bundles.
- **[verbose-tui](https://github.com/thetrung/verbose-tui)** — Ratatui-inspired zero-GC double-buffered 24-bit TrueColor TUI framework with Unicode widgets and zero `ncurses` dependency.
- **[verbose-openSKP](https://github.com/thetrung/verbose-openSKP)** — VerBose-Lang + C ABI wrapper around MIT OpenSKP for loading SketchUp `.skp` scenes (meshes, instanced hierarchy, materials, layers, scenes/pages) directly into flat RAM arrays.
- **[verbose-viewSKP](https://github.com/thetrung/verbose-viewSKP)** — ZBrush-style multithreaded CPU Pixol + Toon/Ink Webtoon `.skp` shot studio built with `verbose-openSKP` and `verbose-raylib` (`v0.7.0` `Operator` overloading, `Break`/`Continue`, `Async`/`Await`).

---

## Lesson Learned

Building all 6 ecosystem repositories (`verbose-raylib`, `verbose.nvim`, `verbose-pkg`, `verbose-tui`, `verbose-openSKP`, `verbose-viewSKP`) from scratch revealed how much VerBose-Lang's intentional simplicity accelerates real-world systems work:

1. **Direct C ABI & `clang` pass-through**: `Structure` layout matches C verbatim (`12/12` `SizeOf(...) == sizeof(...)` checks matched Raylib 6.2 directly) and `verbose` forwards `.c`/`.a`/`-l`/`-I` flags straight to `clang`. That eliminated bindgen/IDL tooling and FFI marshaling glue — ~190 lines of POSIX C (`tui_term.c`) was enough to build a full zero-`ncurses` TUI framework, and wrapping a full C++17 `.skp` parser (`verbose-openSKP`) needed only a flat C ABI bridge.
2. **`Arena` + value `Structure`s + `With` blocks + `Operator` overloading**: Deterministic `Arena` allocation and value structs remove borrow-checker ceremony, header/source duplication, and GC-FFI coordination, while `v0.6.0`–`v0.7.0` ergonomics (`Match ... With`, `$"..."`, `With`, grouped `Dim`, `For ... Do`, `Break`/`Continue`, `Structure` `Operator` overloading) keep math-heavy and systems code compact and readable.
3. **Regular, explicit, line-oriented syntax**: Unambiguous declarations (`Include`, `Structure`, `Enum`, `Function`, `Sub`, `Declare`, `Dim x As Type`) made it straightforward to build both a 100%-parsing Tree-sitter grammar and a zero-dependency in-process Neovim LSP, while the flat `Include "..."` + `-I` model enabled a completely serverless package manager (`vpm`).
4. **Built-in `Test ... End Test` + fast native compiles**: Self-contained unit tests with zero external test frameworks and sub-second compile-run cycles made iterating across libraries and cross-package integration feel like a scripting language with native C performance.

---

## Roadmap

See **[TODO.md](TODO.md)** for the active roadmap and future extensions, and **[LOGS.md](LOGS.md)** for completed tasks and milestone history.
