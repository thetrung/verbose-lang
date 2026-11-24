# VerBose-Lang — Roadmap & TODO

Active roadmap and completed milestones for **VerBose-Lang**.

## Active TODO

_All planned roadmap milestones are complete! New ideas and next-phase items will be tracked here._

---

## Completed Milestones

- [x] **Language Extensions**: Added structured concurrency (`Async Function`/`Sub`, `Task(T)`/`Task`, `Await` — POSIX `pthread` parallelism on Linux/macOS and zero-dependency deferred thunks on freestanding `arm-m`) and low-level memory/control primitives (`AddressOf`, `PtrRead`, `PtrWrite`, `PtrAdd`, `Label`, `GoTo`)
- [x] **Cross-Compilation & Target Triples**: Emitted explicit LLVM target triples and data layouts for Linux x86-64, macOS arm64, and bare-metal 32-bit ARM-M (Thumbv7EM), zero clang `-Woverride-module` warnings, added `-c`/`--emit-obj`, and added `Volatile` variables, fixed-address `At <addr>` MMIO registers, and `VolatileLoad`/`VolatileStore` primitives
- [x] **Standard Library (`std/`)**: Built core Arena-backed modules in pure VerBose: `std/strings.vb` (`Str*`, `StrView`, `StrBuilder`), `std/math.vb` (100% pure VerBose, zero `-lm`), `std/collections.vb` (`IntVec`, `PtrVec`, `IntRing`, `IntMap`), and `std/io.vb` (`Io*` file and console operations)
- [x] **Self-Hosted Semantic Type Checker & CLI**: Ported semantic Type Analyzer (`src/type_analyzer.vb`, `--ta`/`--no-ta`), AST printer (`src/printer.vb`, `--ast`), IR printer (`--ir`), and full CLI driver (`src/verbose.vb`) with direct compile-and-execute by default
- [x] **Tooling & CI**: Added `make install`/`uninstall`/`dist` packaging and lightweight GitHub Actions CI + automated Linux x86-64 binary release workflows (`.github/workflows/ci.yml`, `.github/workflows/release.yml`)
- [x] **Brace-Free Array Syntax, Stage 0 Archival & Repo Cleanup**: Replaced `{..}` array literals with `(..)` across Stage 0 and self-hosted compilers, archived `stage0/` onto the `stage0` git branch and removed it from `main`, and extracted `FEATURES.md`
- [x] **Built-in `Test` / `Assert` & Zero-Python Testing**: Added first-class `Test "name" ... End Test` blocks, `Assert cond [, "msg"]` statements, and `./verbose test [file.vb | dir]` CLI runner; consolidated `examples/` into `tests/`, replaced all Python test runners with pure-VerBose suites, and removed `python3` from CI/Release workflows
- [x] **Compress Language Syntax & Compiler Ergonomics**: Added grouped same-type & mixed-type declarations (`Dim`, `Static`, `Public`/`Private Static`, `Structure` fields, `Function`/`Sub`/`Declare` parameters), shared `src/constants.vb` module for compiler diagnostics & LLVM IR runtime templates, and native string concatenation (`&` and `+` on `Pointer`/`Byte` arrays with compile-time folding, multiline `&` continuations, and lazy runtime `@__vb_str_concat`), and refactored the compiler & tests to dogfood the compressed syntax
