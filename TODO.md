# VerBose-Lang — Roadmap & TODO

Active roadmap and completed milestones for **VerBose-Lang**.

## Active TODO

- [ ] **Compress Language Syntax & Compiler Ergonomics**:
  - Allow common declarations (`Dim`, `Static`, `Public Static`, `Private Static`, `Structure` fields, and `Function`/`Sub`/`Declare` parameters) to group same-type variables (e.g. `Dim a, b, c As Integer`, `Dim x = 1, y = 2 As Integer`, mixed-type comma clauses, multiline comma continuations)
  - Move compiler diagnostic/message logs and LLVM IR runtime templates into a shared `src/constants.vb` module
  - Add native string concatenation (`&` and `+` on string/`Pointer` expressions) with compile-time constant folding for string literals and lazy runtime `@__vb_str_concat` lowering
- [ ] **Cross-Compilation & Target Triples**: Emit explicit LLVM target triples & data layouts for Linux x86-64, macOS arm64, and bare-metal 32-bit ARM-M (incl. `Volatile` / fixed memory regions)
- [ ] **Standard Library (`std/`)**: Build core modules (`strings`, `math`, `collections`, `io`) using `Include` + `Arena`
- [ ] **Language Extensions**: Structured concurrency (`Async`/`Await`) and low-level address/label/raw memory primitives

---

## Completed Milestones

- [x] **Self-Hosted Semantic Type Checker & CLI**: Ported semantic Type Analyzer (`src/type_analyzer.vb`, `--ta`/`--no-ta`), AST printer (`src/printer.vb`, `--ast`), IR printer (`--ir`), and full CLI driver (`src/verbose.vb`) with direct compile-and-execute by default
- [x] **Tooling & CI**: Added `make install`/`uninstall`/`dist` packaging and lightweight GitHub Actions CI + automated Linux x86-64 binary release workflows (`.github/workflows/ci.yml`, `.github/workflows/release.yml`)
- [x] **Brace-Free Array Syntax, Stage 0 Archival & Repo Cleanup**: Replaced `{..}` array literals with `(..)` across Stage 0 and self-hosted compilers, archived `stage0/` onto the `stage0` git branch and removed it from `main`, and extracted `FEATURES.md`
- [x] **Built-in `Test` / `Assure` & Zero-Python Testing**: Added first-class `Test "name" ... End Test` blocks, `Assure cond [, "msg"]` statements, and `./verbose test [file.vb | dir]` CLI runner; consolidated `examples/` into `tests/`, replaced all Python test runners with pure-VerBose suites, and removed `python3` from CI/Release workflows
