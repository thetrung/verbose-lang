' Raylib 5.x FFI bindings, C ABI struct lowering & headless demo test suite.

Include "harness.vb"

Private Sub MakeTmpFile(dst As Pointer, cap As Integer, tag As Pointer)
  Dim pid As Integer = getpid()
  If IsWindowsHost() Then
    snprintf(dst, cap, "%s\\vb_raylib_%d_%s", GetTempDir(), pid, tag)
  Else
    snprintf(dst, cap, "%s/vb_raylib_%d_%s", GetTempDir(), pid, tag)
  End If
End Sub

Test "raylib: bouncing_ball.vb headless shim game loop & Arena scratch reset"
  Assert RunVb("tests/raylib/bouncing_ball.vb", "tests/raylib/raylib_headless_shim.c -lm") = 0
  Assert OutHas("InitWindow(800,450,VerBose + Raylib Demo)")
  Assert OutHas("SetTargetFPS(60)")
  Assert OutHas("Frame 1 ClearBackground(24,24,36,255)")
  Assert OutHas("Frame 1 DrawRectangleRec(150.0,410.0,100.0,16.0 rgba=80,200,120,255)")
  Assert OutHas("Frame 1 DrawCircleV(780.0,230.0 r=20.0 rgba=230,41,55,255)")
  Assert OutHas("Frame 1 DrawText(VerBose-Lang + Raylib @16,16 sz=20 rgba=240,240,240,255)")
  Assert OutHas("Frame 2 DrawRectangleRec(175.0,410.0,100.0,16.0 rgba=80,200,120,255)")
  Assert OutHas("Frame 2 DrawCircleV(720.0,260.0 r=20.0 rgba=230,41,55,255)")
  Assert OutHas("Frame 3 DrawRectangleRec(200.0,410.0,100.0,16.0 rgba=80,200,120,255)")
  Assert OutHas("Frame 3 DrawCircleV(660.0,290.0 r=20.0 rgba=230,41,55,255)")
  Assert OutHas("CloseWindow(frames=3)")
End Test

Test "raylib: bouncing_ball.vb passes strict --ta Type Analyzer"
  Dim comp(768), llOut(256), cmd(2048) As Byte
  GetCompilerAbs(comp, 768)
  MakeTmpFile(llOut, 256, "ta.ll")
  snprintf(cmd, 2048, "\"%s\" --ta --emit-only tests/raylib/bouncing_ball.vb -o \"%s\"", comp, llOut)
  Dim rc As Integer = RunShell(cmd)
  Dim exists As Boolean = FileExists(llOut)
  unlink(llOut)
  Assert rc = 0, "bouncing_ball.vb failed --ta Type Analyzer"
  Assert exists, "expected LLVM IR output file"
End Test

Test "raylib: Color/Vector2/Rectangle SizeOf, MakeColor, GetMousePosition, IsKeyDown & 5-arg DrawText ABI"
  Dim srcPath(256) As Byte
  MakeTmpFile(srcPath, 256, "abi.vb")
  WriteTextFile(srcPath, "Include \"raylib.vb\"\nPublic Function main() As Integer\n  Dim szC As Integer = SizeOf(Color)\n  Dim szV As Integer = SizeOf(Vector2)\n  Dim szR As Integer = SizeOf(Rectangle)\n  Dim c As Color = MakeColor(11, 22, 33, 255)\n  BeginDrawing()\n  Dim m1 As Vector2 = GetMousePosition()\n  Dim kSpace As Boolean = IsKeyDown(32)\n  Dim kOther As Boolean = IsKeyDown(65)\n  Dim r As Rectangle With ( x = 12.5, y = 24.5, width = 80.0, height = 40.0 )\n  DrawRectangleRec(r, c)\n  DrawText(\"ABI-OK\", 5, 10, 18, c)\n  EndDrawing()\n  Printf(\"sizes=%d,%d,%d mouse=%.1f,%.1f keys=%d,%d\\n\", szC, szV, szR, m1.x, m1.y, kSpace, kOther)\n  Return 0\nEnd Function\n")
  Dim rc As Integer = RunVb(srcPath, "-I tests/raylib tests/raylib/raylib_headless_shim.c -lm")
  unlink(srcPath)
  Assert rc = 0, "Raylib ABI test program failed to compile/run"
  Assert OutHas("Frame 1 DrawRectangleRec(12.5,24.5,80.0,40.0 rgba=11,22,33,255)")
  Assert OutHas("Frame 1 DrawText(ABI-OK @5,10 sz=18 rgba=11,22,33,255)")
  Assert OutHas("sizes=4,8,16 mouse=225.0,300.0 keys=1,0")
End Test

Test "raylib: cross-target -c object compilation (linux-x86_64, linux-arm64, macos-arm64, windows-x86_64)"
  Dim comp(768), objPath(256), cmd(2048) As Byte
  GetCompilerAbs(comp, 768)

  MakeTmpFile(objPath, 256, "lx64.o")
  snprintf(cmd, 2048, "\"%s\" --target linux-x86_64 -c tests/raylib/bouncing_ball.vb -o \"%s\"", comp, objPath)
  Dim rc1 As Integer = RunShell(cmd)
  Dim ex1 As Boolean = FileExists(objPath)
  unlink(objPath)
  Assert rc1 = 0, "linux-x86_64 -c failed for bouncing_ball.vb"
  Assert ex1, "missing linux-x86_64 .o"

  MakeTmpFile(objPath, 256, "larm64.o")
  snprintf(cmd, 2048, "\"%s\" --target linux-arm64 -c tests/raylib/bouncing_ball.vb -o \"%s\"", comp, objPath)
  Dim rc2 As Integer = RunShell(cmd)
  Dim ex2 As Boolean = FileExists(objPath)
  unlink(objPath)
  Assert rc2 = 0, "linux-arm64 -c failed for bouncing_ball.vb"
  Assert ex2, "missing linux-arm64 .o"

  MakeTmpFile(objPath, 256, "marm64.o")
  snprintf(cmd, 2048, "\"%s\" --target macos-arm64 -c tests/raylib/bouncing_ball.vb -o \"%s\"", comp, objPath)
  Dim rc3 As Integer = RunShell(cmd)
  Dim ex3 As Boolean = FileExists(objPath)
  unlink(objPath)
  Assert rc3 = 0, "macos-arm64 -c failed for bouncing_ball.vb"
  Assert ex3, "missing macos-arm64 .o"

  MakeTmpFile(objPath, 256, "wx64.o")
  snprintf(cmd, 2048, "\"%s\" --target windows-x86_64 -c tests/raylib/bouncing_ball.vb -o \"%s\"", comp, objPath)
  Dim rc4 As Integer = RunShell(cmd)
  Dim ex4 As Boolean = FileExists(objPath)
  unlink(objPath)
  Assert rc4 = 0, "windows-x86_64 -c failed for bouncing_ball.vb"
  Assert ex4, "missing windows-x86_64 .o"
End Test
