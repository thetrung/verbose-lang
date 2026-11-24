' C Interop, SysV ABI structs, Raylib demo, escapes & unary minus regression suite.

Include "harness.vb"

Test "01_libm_and_unary_neg.vb — libm FFI (-lm), Single/Double coercion, unary negation"
  Assure RunVb("test/c_interop/01_libm_and_unary_neg.vb", "-lm") = 0
  Assure OutHas("sqrt=4.0 pow=1024.0 sinf=0.0 cos=1.0 neg=-4.0,-42")
End Test

Test "02_large_c_struct_abi.vb — Large >16B C struct byval(%struct) & ByRef mutation"
  Assure RunVb("test/c_interop/02_large_c_struct_abi.vb", "test/c_interop/big_struct_helper.c") = 0
  Assure OutHas("s1=60 s2=180 fields=30,60,90")
End Test

Test "03_escapes_and_select_return.vb — String escapes & early Return inside Select Case"
  Assure RunVb("test/c_interop/03_escapes_and_select_return.vb", "") = 0
  Assure OutHas("esc=\"hello\"\\t\tpath=C:\\vb")
  Assure OutHas("cls=100,200,-1")
End Test

Test "examples/raylib/bouncing_ball.vb — Raylib C ABI + Arena + Include headless demo"
  Assure RunVb("examples/raylib/bouncing_ball.vb", "examples/raylib/raylib_headless_shim.c -lm") = 0
  Assure OutHas("InitWindow(800,450,VerBose + Raylib Demo)")
  Assure OutHas("SetTargetFPS(60)")
  Assure OutHas("Frame 1 ClearBackground(24,24,36,255)")
  Assure OutHas("Frame 1 DrawRectangleRec(150.0,410.0,100.0,16.0 rgba=80,200,120,255)")
  Assure OutHas("Frame 1 DrawCircleV(780.0,230.0 r=20.0 rgba=230,41,55,255)")
  Assure OutHas("Frame 2 DrawCircleV(720.0,260.0 r=20.0 rgba=230,41,55,255)")
  Assure OutHas("Frame 3 DrawCircleV(660.0,290.0 r=20.0 rgba=230,41,55,255)")
  Assure OutHas("CloseWindow(frames=3)")
End Test
