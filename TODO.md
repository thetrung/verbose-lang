# VerBose-Lang — Roadmap & TODO

Active roadmap and future extensions for **VerBose-Lang**. Completed tasks and milestones are archived in **[LOGS.md](LOGS.md)**.

## Active TODO

### Milestone 1: Generics (Monomorphized Parametric `Structure`, `Function`, `Sub`, `Operator`)
- [x] Completed in `v0.8.0` and archived in **[LOGS.md](LOGS.md)**.

### Milestone 2: Tagged Unions / Sum Types & `Result`-Style Error Handling
- [x] Completed in `v0.9.0` and archived in **[LOGS.md](LOGS.md)**.

### Milestone 3: Deterministic `Defer` Statement
- [x] Completed in `v0.10.0` and archived in **[LOGS.md](LOGS.md)**.

### Milestone 4: Typed Function Pointers
- [x] Completed in `v0.10.0` and archived in **[LOGS.md](LOGS.md)**.

### Milestone 5: Debug Info (`-g` DWARF) & Concurrency / String Ownership Ergonomics
- [ ] Emit LLVM DWARF debug metadata (`!DICompileUnit`, `!DISubprogram`, `!DILocation`) behind `-g` / `--debug` so GDB/LLDB show `.vb` file names, line numbers, and readable stack traces.
- [ ] Add pure-VerBose synchronization & concurrency primitives in `std/sync.vb` (mutexes, atomics, spinlocks/barriers) beyond 1:1 `Async`/`Await` tasks.
- [ ] Clarify and improve string ownership ergonomics (arena-backed string formatting/concatenation helpers alongside existing `calloc`-backed `&`/`$"..."`).

## Extensions

_All completed ecosystem extensions (`verbose-raylib`, `verbose.nvim`, `verbose-pkg`, `verbose-tui`, `verbose-openSKP`, `verbose-viewSKP`, `verbose-sqlite3`, `verbose-curl`) have been moved to **[LOGS.md](LOGS.md)**._
