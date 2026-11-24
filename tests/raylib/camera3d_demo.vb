' Raylib 3D Camera + Cube Demo written in VerBose-Lang
' Demonstrates passing 12-byte (Vector3) and 44-byte (Camera3D) structs ByVal across the C ABI.
'
' Build with real Raylib:
'   ./verbose tests/raylib/camera3d_demo.vb -lraylib -lm
' Build & run headless verification shim (no Raylib install required):
'   ./verbose tests/raylib/camera3d_demo.vb tests/raylib/raylib_headless_shim.c -lm

Include "raylib.vb"

Public Function main() As Integer
  InitWindow(800, 450, "VerBose + Raylib 3D Camera")
  SetTargetFPS(60)

  Dim bg As Color = MakeColor(18, 22, 32, 255)
  Dim cubeColor As Color = MakeColor(230, 41, 55, 255)
  Dim textColor As Color = MakeColor(240, 240, 240, 255)

  ' 44-byte Camera3D passed ByVal directly to Raylib's BeginMode3D
  Dim cam As Camera3D With (
    position = MakeVec3(0.0, 10.0, 10.0),
    target = MakeVec3(0.0, 0.0, 0.0),
    up = MakeVec3(0.0, 1.0, 0.0),
    fovy = 45.0,
    projection = CameraProjection.CAMERA_PERSPECTIVE
  )

  Dim cubePos As Vector3 = MakeVec3(0.0, 1.0, 0.0)

  While Not WindowShouldClose() Do
    Dim dt As Single = GetFrameTime()
    cam.position.x = cam.position.x + 2.0 * dt

    BeginDrawing()
    ClearBackground(bg)
    BeginMode3D(cam)
    DrawCube(cubePos, 2.0, 2.0, 2.0, cubeColor)
    DrawGrid(10, 1.0)
    EndMode3D()
    DrawText("VerBose-Lang 3D Camera", 16, 16, 20, textColor)
    EndDrawing()
  End While

  CloseWindow()
  Return 0
End Function
