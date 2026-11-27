# Compiler CLI Flags

Command-line interface options and usage reference for `./verbose`.

### Compiler CLI Flags (`./verbose`)

| Flag | Description |
| --- | --- |
| `verbose <file.vb>` | Type-check, emit `.ll`, compile with `clang -O2`, and **execute directly** |
| `verbose test [file.vb \| dir]` | Compile & run built-in `Test`/`Assert` blocks (auto-discovers `test_*.vb`/`*_test.vb` in directories; default `tests`; forwards `-I`, `-l`, `-L`, `-W`, and extra `.c/.o/.a` inputs to `clang`) |
| `--test` | Alias for `verbose test` |
| `--target <name>`, `--target=<name>` | Set compilation target architecture/triple (`linux-x86_64`, `linux-arm64`, `macos-arm64`, `windows-x86_64`, `arm-m`; defaults to auto-detected host target, overrideable via `VERBOSE_TARGET`). Aliases include `x86_64-pc-linux-gnu`, `aarch64-unknown-linux-gnu`, `arm64-apple-darwin`, `win64`, `x86_64-w64-windows-gnu`, `cortex-m4`, `thumbv7em-unknown-none-eabi`. Emits target-specific datalayout & triple, selects target C ABI lowering, and adjusts pointer size (4 on `arm-m`, 8 on 64-bit). |
| `-c`, `--emit-obj` | Compile LLVM IR to native object file (`.o`) via `clang -O2 -c` without linking or executing (auto-selected for `arm-m` and non-host cross-targets unless `--emit-only`) |
| `--ast` | Pretty-print the parsed AST |
| `--ir` | Print the generated LLVM IR to stdout |
| `--ta` | Verbose semantic Type Analyzer output (runs automatically before codegen) |
| `--no-ta` | Skip the semantic Type Analyzer pass |
| `--bounds-check` | Enable explicit opt-in runtime bounds checking (`0 <= idx < N`) on fixed-size arrays (`arrLen > 0`); prints error & aborts on hosted targets or emits `llvm.trap` on freestanding `arm-m`. Off by default (zero hidden overhead in release builds). |
| `--max-stack <bytes>`, `--max-stack=<bytes>` | Maximum allowed stack frame size per function/operator/test in bytes (enforced as a compile-time error; defaults to `65536` on hosted 64-bit targets and `4096` on bare-metal `arm-m`; `0` disables) |
| `-g`, `--debug` | Emit LLVM DWARF debug metadata (`!DICompileUnit`, `!DIFile`, `!DISubprogram`, `!DILocation`) mapping instructions to `.vb` files & line numbers, and compile with `clang -O0 -g` for GDB/LLDB debugging |
| `--emit-only` | Emit LLVM IR (`.ll`) only; skip `clang` compilation and execution |
| `--no-run` | Compile native executable with `clang`, skip execution |
| `-o <path>` | Output binary path (or `.ll` path with `--emit-only`, or `.o` path with `-c`) |
| `-I <dir>` / `-I<dir>` | Add `Include` module search directory (current working directory `.` is searched as fallback) |
| `-l<lib>` / `-L<dir>` / `-W<flag>` | Forward linker/warning flags to `clang` (e.g. `-lm`, `-lraylib`) |
| `<file.c/.o/.a>` | Additional C/object/archive files compiled & linked alongside `.vb` |
| `-h`, `--help`, `--version` | Show CLI usage or compiler version |
