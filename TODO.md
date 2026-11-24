# VerBose-Lang — Roadmap & TODO

Active roadmap and completed milestones for **VerBose-Lang**.

## Active TODO
_All planned roadmap milestones are complete! New ideas and next-phase items will be tracked here.

1. [x] **Explain how `verbose test`** work : in **FEATURES.md** (how it link c-lib too) along how to write one (usage).
2. [x] **Refine `tests/raylib/`** : refactor the demo to be compile-able with comment/note text file along.
3. [x] **String Interpolation** : alike to **VBNET 14+** style: `Dim result As String = $"{ quantity } x { item_names }"`, add & test feature.
4. [x] **Pattern Matching** : like OCAML, it could either match value, variable, Conditional statement.. etc like : 
```
Match condition With  
  Case True Then Return 1 
  Case False Then
    DoSomeThing()
    Return 0
End Match
```
As the goal is to reduce redundant code like dozen of `If..Then..End If` since this Pattern Matching will handle `Case ... Then` Block, write test-case then refactor the codebase with it.
5. [ ] **Language Syntax / Semantic Compression** : analyze whole codebase to suggest new features or combine any unnecessary features to shorten amount of keyword/lines of code in codebase.

## Extensions 
- [ ] **Raylib Wrapper Support** : (in new repo) attempt to build a wrapper in c/verbose to build up functions natively in Verbose-Lang style for more intensive graphical demos.
- [ ] **Package Manager** : (in new repo) Learn from what you have done with Raylib to build a way to pack such library so people can pack, upload/download, install into their project like cargo did while easy to include/import in verbose. I don't mean to build a whole server thing but a way to pack/build/install from source code/binary. We may discuss this if you are unclear or confused.
- [ ] **NVIM Plugin** : (in new repo) syntax-highlighting, intellisense (cache on included/imported files), auto-complete, treesitter, include latest version of verbose for easy installation.
- [ ] **TUI support** : (in new repo) lookup ways to build TUI application with verbose like Ratatui on Rust.

## Completed Milestones

- [x] **Multi-Platform Native Releases (`v0.5.1`)**: Added `linux-arm64` and `windows-x86_64` targets with host auto-detection (`VERBOSE_TARGET`), target-native C ABI struct lowering (SysV x86-64, AAPCS64, Microsoft x64, ARM AAPCS), C variadic default argument promotions (`Single→Double`, `Boolean/Byte/Short→i32`), Windows `\`/`<drive>:` `Include` path handling, dedicated `tests/test_raylib.vb` suite (19/19 suites, 151 `Test` blocks), and automated 5-job GitHub Releases publishing native binaries for Linux x86-64, Linux arm64, macOS arm64, and Windows x86-64 (`SHA256SUMS.txt`)
- [x] **Language Extensions**: Added structured concurrency (`Async Function`/`Sub`, `Task(T)`/`Task`, `Await` — `pthread` parallelism on Linux/macOS/Windows MSYS2 and zero-dependency deferred thunks on freestanding `arm-m`) and low-level memory/control primitives (`AddressOf`, `PtrRead`, `PtrWrite`, `PtrAdd`, `Label`, `GoTo`)
- [x] **Cross-Compilation & Target Triples**: Emitted explicit LLVM target triples and data layouts for Linux x86-64, Linux arm64, macOS arm64, Windows x86-64, and bare-metal 32-bit ARM-M (Thumbv7EM), zero clang `-Woverride-module` warnings, added `-c`/`--emit-obj`, and added `Volatile` variables, fixed-address `At <addr>` MMIO registers, and `VolatileLoad`/`VolatileStore` primitives
- [x] **Standard Library (`std/`)**: Built core Arena-backed modules in pure VerBose: `std/strings.vb` (`Str*`, `StrView`, `StrBuilder`), `std/math.vb` (100% pure VerBose, zero `-lm`), `std/collections.vb` (`IntVec`, `PtrVec`, `IntRing`, `IntMap`), and `std/io.vb` (`Io*` file and console operations)
- [x] **Self-Hosted Semantic Type Checker & CLI**: Ported semantic Type Analyzer (`src/type_analyzer.vb`, `--ta`/`--no-ta`), AST printer (`src/printer.vb`, `--ast`), IR printer (`--ir`), and full CLI driver (`src/verbose.vb`) with direct compile-and-execute by default
- [x] **Tooling & CI**: Added `make install`/`uninstall`/`dist` packaging and lightweight GitHub Actions CI + automated Linux x86-64 binary release workflows (`.github/workflows/ci.yml`, `.github/workflows/release.yml`)
- [x] **Brace-Free Array Syntax, Stage 0 Archival & Repo Cleanup**: Replaced `{..}` array literals with `(..)` across Stage 0 and self-hosted compilers, archived `stage0/` onto the `stage0` git branch and removed it from `main`, and extracted `FEATURES.md`
- [x] **Built-in `Test` / `Assert` & Zero-Python Testing**: Added first-class `Test "name" ... End Test` blocks, `Assert cond [, "msg"]` statements, and `./verbose test [file.vb | dir]` CLI runner; consolidated `examples/` into `tests/`, replaced all Python test runners with pure-VerBose suites, and removed `python3` from CI/Release workflows
- [x] **Compress Language Syntax & Compiler Ergonomics**: Added grouped same-type & mixed-type declarations (`Dim`, `Static`, `Public`/`Private Static`, `Structure` fields, `Function`/`Sub`/`Declare` parameters), shared `src/constants.vb` module for compiler diagnostics & LLVM IR runtime templates, and native string concatenation (`&` and `+` on `Pointer`/`Byte` arrays with compile-time folding, multiline `&` continuations, and lazy runtime `@__vb_str_concat`), and refactored the compiler & tests to dogfood the compressed syntax
