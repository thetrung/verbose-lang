' C Interop, SysV ABI structs, Raylib demo, escapes & unary minus regression suite.

Include "harness.vb"

Test "01_libm_and_unary_neg.vb — libm FFI (-lm), Single/Double coercion, unary negation"
  Assert RunVb("tests/c_interop/01_libm_and_unary_neg.vb", "-lm") = 0
  Assert OutHas("sqrt=4.0 pow=1024.0 sinf=0.0 cos=1.0 neg=-4.0,-42")
End Test

Test "02_large_c_struct_abi.vb — Large >16B C struct byval(%struct) & ByRef mutation"
  Assert RunVb("tests/c_interop/02_large_c_struct_abi.vb", "tests/c_interop/big_struct_helper.c") = 0
  Assert OutHas("s1=60 s2=180 fields=30,60,90")
End Test

Test "03_escapes_and_select_return.vb — String escapes & early Return inside Select Case"
  Assert RunVb("tests/c_interop/03_escapes_and_select_return.vb", "") = 0
  Assert OutHas("esc=\"hello\"\\t\tpath=C:\\vb")
  Assert OutHas("cls=100,200,-1")
End Test

Test "tests/raylib/bouncing_ball.vb — Raylib C ABI + Arena + Include headless demo"
  Assert RunVb("tests/raylib/bouncing_ball.vb", "tests/raylib/raylib_headless_shim.c -lm") = 0
  Assert OutHas("InitWindow(800,450,VerBose + Raylib Demo)")
  Assert OutHas("SetTargetFPS(60)")
  Assert OutHas("Frame 1 ClearBackground(24,24,36,255)")
  Assert OutHas("Frame 1 DrawRectangleRec(150.0,410.0,100.0,16.0 rgba=80,200,120,255)")
  Assert OutHas("Frame 1 DrawCircleV(780.0,230.0 r=20.0 rgba=230,41,55,255)")
  Assert OutHas("Frame 2 DrawCircleV(720.0,260.0 r=20.0 rgba=230,41,55,255)")
  Assert OutHas("Frame 3 DrawCircleV(660.0,290.0 r=20.0 rgba=230,41,55,255)")
  Assert OutHas("CloseWindow(frames=3)")
End Test
