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

**Prerequisites**: `clang` (LLVM 19+ recommended) and `curl` (only if bootstrapping without an existing `verbose` binary). Prebuilt native binaries for **Linux x86-64**, **Linux arm64**, **macOS arm64**, and **Windows x86-64** are published on [GitHub Releases](https://github.com/thetrung/verbose-lang/releases/latest). No OCaml or Python required!

```bash
# Build & verify the self-hosted compiler (./verbose) via Stage 1 -> Stage 2 -> Stage 3 fixpoint
# Uses existing ./verbose, `verbose` in PATH, BOOTSTRAP=/path/to/verbose, or downloads the latest GitHub Release binary
make build

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

# Build and run prototype.vb with ./verbose
make

# Install ./verbose globally (default: /usr/local/bin/verbose) or build release artifacts
make install          # or: make install PREFIX=$HOME/.local
make dist             # builds stripped platform binary in dist/ + dist/SHA256SUMS.txt

# Run pure-VerBose test suites (zero Python!)
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
- Fast as C/Rust with zero GC, native C-interop, and deterministic memory (`Arena`, `Static`, `Heap`, raw `Pointer`).
- Portable across LLVM targets — Linux (x86-64, arm64), macOS (arm64), Windows (x86-64), and bare-metal 32-bit ARM-M microcontrollers.

---

## Roadmap

See **[TODO.md](TODO.md)** for the active roadmap and completed milestones.
