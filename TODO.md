# VerBose-Lang — Roadmap & TODO

Active roadmap and future extensions for **VerBose-Lang**. Completed tasks and milestones are archived in **[LOGS.md](LOGS.md)**.

## Active TODO

### Milestone 1: Generics (Monomorphized Parametric `Structure`, `Function`, `Sub`, `Operator`)
- [x] Completed in `v0.8.0` and archived in **[LOGS.md](LOGS.md)**.

### Milestone 2: Tagged Unions / Sum Types & `Result`-Style Error Handling
- [ ] Add algebraic data types / tagged unions (e.g., `Union` / payload-carrying `Enum` variants) with zero-GC inline tagged memory layout.
- [ ] Extend `Match ... With` to destructure tagged union variants with bindings and compile-time exhaustiveness checking.
- [ ] Provide ergonomic `Option(Of T)` / `Result(Of T, E)` patterns (building on Milestone 1 generics) to replace manual sentinel return codes.

### Milestone 3: Deterministic `Defer` Statement
- [ ] Add lexical-scope `Defer <stmt>` (executed in LIFO order on normal block exit, `Return`, `Break`, and `Continue`) for deterministic `HeapFree`, `ArenaDestroy`, and file/handle cleanup with zero GC or hidden RAII.
- [ ] Validate control-flow edge cases in Type Analyzer (no `Return`/`Break`/`Continue`/`GoTo` escaping a `Defer` body) and lower cleanly in `codegen.vb`.

### Milestone 4: Typed Function Pointers
- [ ] Add first-class typed function pointer types (e.g. `FuncPtr(params) As Ret` / `SubPtr(params)`) so `AddressOf(fn)` and C FFI callbacks are type-checked instead of falling back to untyped `Pointer`.
- [ ] Support indirect calls through typed function pointer variables, struct fields, and parameters with full C ABI parity across all targets.

### Milestone 5: Debug Info (`-g` DWARF) & Concurrency / String Ownership Ergonomics
- [ ] Emit LLVM DWARF debug metadata (`!DICompileUnit`, `!DISubprogram`, `!DILocation`) behind `-g` / `--debug` so GDB/LLDB show `.vb` file names, line numbers, and readable stack traces.
- [ ] Add pure-VerBose synchronization & concurrency primitives in `std/sync.vb` (mutexes, atomics, spinlocks/barriers) beyond 1:1 `Async`/`Await` tasks.
- [ ] Clarify and improve string ownership ergonomics (arena-backed string formatting/concatenation helpers alongside existing `calloc`-backed `&`/`$"..."`).

### Compiler Literal & Float Quality-of-Life Fixes (Discovered building `std/json.vb`)
- [ ] **64-bit integer literals (`AstNode.intVal As Long`)**: Widen integer literal parsing and AST storage from 32-bit `Integer` to 64-bit `Long` so literals `> 2^31 - 1` (e.g. `9223372036854775807`, `1000000000000`) do not silently truncate.
- [ ] **Scientific notation float literals**: Support `e`/`E` exponents (`1.0e-4`, `1.7976931348623157e308`) in `src/lexer.vb`.
- [ ] **IEEE-754 unordered float `<>`**: Emit `fcmp une` (instead of `fcmp one`) for floating-point `<>` in `src/codegen.vb` so `NaN <> NaN` evaluates to `True` per IEEE-754.

## Extensions

_All completed ecosystem extensions (`verbose-raylib`, `verbose.nvim`, `verbose-pkg`, `verbose-tui`, `verbose-openSKP`, `verbose-viewSKP`, `verbose-sqlite3`) have been moved to **[LOGS.md](LOGS.md)**._
