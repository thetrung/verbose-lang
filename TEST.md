# VerBose-Lang — Built-in Testing (`verbose test`)

VerBose-Lang ships with a zero-dependency native test runner built directly into the compiler (`./verbose test` or `./verbose --test`).

## How It Works

1. **Normal mode vs. test mode**:
   - Running `./verbose file.vb` compiles a regular program expecting `Public Function main(...) As Integer`. In normal mode, `Assert cond [, "msg"]` acts as a runtime check that prints the source line and optional message and exits with status `1` on failure.
   - Running `./verbose test file.vb` switches the code generator into test mode. Instead of requiring `main()`, the compiler collects every top-level `Test "name" ... End Test` block, emits each block as an isolated test function, and synthesizes a native `main()` test harness in LLVM IR.
2. **Failure isolation & cleanup**:
   - Inside test mode, a failed `Assert` prints the failing line and message, marks the current test as failed, and returns early from that test block without aborting the process — so remaining `Test` blocks in the file still run.
   - The synthesized harness prints `[PASS]` / `[FAIL]` for each test plus a summary line and exits `0` only if every test passes.
   - Temporary `.ll` and test binaries are written to `/tmp` (or `%TEMP%` on Windows) and deleted automatically after the run.
3. **Suite discovery**:
   - `./verbose test` with no path scans `tests/` (equivalent to `./verbose test tests/`).
   - Passing a directory discovers all `test_*.vb` and `*_test.vb` files in that directory (sorted), compiles and runs each suite independently, and reports an aggregate summary.
   - You can also pass one or more individual files or directories: `./verbose test tests/test_std_io.vb tests/test_raylib.vb`.

## Writing a Test File

Test files do not need a `main()` function — put your assertions inside top-level `Test "description" ... End Test` blocks:

```vb
Include "std/math.vb"

Private Function Square(x As Integer) As Integer
  Return x * x
End Function

Test "Square computes x^2"
  Assert Square(0) = 0
  Assert Square(-4) = 16, "negative input should produce positive square"
  Assert MathMaxI(3, 9) = 9
End Test
```

Run it with:

```bash
./verbose test my_test.vb
```

> **Note:** Running `./verbose my_test.vb` directly (without `test`) will fail at link time with an undefined `main` symbol unless the file also defines `main()`.

## Linking C Files and Libraries in Tests

`verbose test` forwards include paths (`-I`), extra C/object/archive inputs (`.c`, `.o`, `.a`), and linker/compiler flags (`-L`, `-l`, `-W`) directly to `clang -O2` when building the test executable:

```bash
# Link a C source file (e.g. an FFI helper or mock shim) + libm:
./verbose test tests/my_ffi_test.vb helper.c -lm

# Link an installed C library (static or shared) with custom include/lib dirs:
./verbose test -I tests/raylib -L /usr/local/lib my_raylib_test.vb -lraylib -lm

# Cross-check types or inspect the generated test binary without executing:
./verbose test --ta --no-run -o /tmp/my_test_bin my_test.vb

# Run tests with explicit opt-in runtime array bounds checking (zero overhead when omitted):
./verbose test --bounds-check tests/test_bounds.vb

# Override the per-function stack-frame compile-time limit (default: 64 KB hosted / 4 KB arm-m):
./verbose test --max-stack 16384 my_test.vb
```

For integration tests that invoke the compiler itself against external C shims or libraries (like [`tests/test_raylib.vb`](tests/test_raylib.vb)), see [`tests/harness.vb`](tests/harness.vb) for the `RunVb(srcPath, extraArgs)` helper.
