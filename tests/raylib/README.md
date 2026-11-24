# VerBose-Lang + Raylib 5.x Demos

Native C FFI demos showing VerBose-Lang interoperating directly with **Raylib 5.x** using ABI-accurate `ByVal` structs and zero-GC per-frame `Arena` memory.

## Files

| File | Description |
| :--- | :--- |
| [`raylib.vb`](raylib.vb) | Raylib 5.x struct layouts (`Color`, `Vector2`, `Vector3`, `Rectangle`, `Camera3D`), `CameraProjection` enum, `Declare` FFI bindings, and `MakeColor`/`MakeVec3` helpers. |
| [`bouncing_ball.vb`](bouncing_ball.vb) | 2D bouncing ball + paddle demo featuring a per-frame `ArenaReset` scratch allocator inside the game loop (zero heap churn). |
| [`camera3d_demo.vb`](camera3d_demo.vb) | 3D camera + cube + grid demo demonstrating 12-byte (`Vector3`) and 44-byte (`Camera3D`) `ByVal` C struct passing across the native ABI. |
| [`raylib_headless_shim.c`](raylib_headless_shim.c) | Lightweight C shim matching Raylib 5.x struct layouts and function signatures so demos and CI tests run headlessly without an X11/Wayland display or installed `libraylib`. |

## Running the Demos

### 1. Headless Verification (No Raylib Required)

Runs 3 deterministic frames using [`raylib_headless_shim.c`](raylib_headless_shim.c) and prints every C FFI call with its unpacked struct fields:

```bash
# 2D Bouncing Ball demo
./verbose tests/raylib/bouncing_ball.vb tests/raylib/raylib_headless_shim.c -lm

# 3D Camera demo
./verbose tests/raylib/camera3d_demo.vb tests/raylib/raylib_headless_shim.c -lm
```

### 2. Real Windowed Raylib (`libraylib` Installed)

Link directly against your system's installed Raylib 5.x library:

```bash
# Linux (shared or static libraylib)
./verbose tests/raylib/bouncing_ball.vb -lraylib -lm
./verbose tests/raylib/camera3d_demo.vb -lraylib -lm

# Custom library path (e.g. local Raylib build)
./verbose tests/raylib/bouncing_ball.vb -L/usr/local/lib -lraylib -lm
```

> **Platform notes:**
> - **Linux**: If linking a static `libraylib.a`, you may also need `-lpthread -ldl -lrt -lX11`.
> - **macOS (Apple Silicon)**: Pass `-L/opt/homebrew/lib -lraylib` (or framework flags if linking statically).
> - **Windows (MSYS2 CLANG64)**: Pass `-lraylib -lopengl32 -lgdi32 -lwinmm`.

### 3. Running the Automated Test Suite

```bash
./verbose test tests/test_raylib.vb
```

This verifies both `bouncing_ball.vb` and `camera3d_demo.vb` headlessly, checks strict `--ta` semantic type analysis, validates all C struct sizes/ABI round-trips, and cross-compiles object files (`-c`) for `linux-x86_64`, `linux-arm64`, `macos-arm64`, and `windows-x86_64`.

## C ABI Struct Layout Notes

| Struct | Size | Alignment | Notes |
| :--- | :---: | :---: | :--- |
| `Color` | 4 B | 1 B | 4 × `Byte` (`r, g, b, a`) |
| `Vector2` | 8 B | 4 B | 2 × `Single` (`x, y`) — passed/returned in registers on 64-bit ABIs |
| `Vector3` | 12 B | 4 B | 3 × `Single` (`x, y, z`) |
| `Rectangle` | 16 B | 4 B | 4 × `Single` (`x, y, width, height`) |
| `Camera3D` | 44 B | 4 B | 3 × `Vector3` + `Single` + `Integer` — lowered via target-native indirect/`byval` ABI |
