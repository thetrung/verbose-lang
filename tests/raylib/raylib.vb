' Raylib 5.x FFI Bindings for VerBose-Lang
' Usage: Include "raylib.vb" and link with -lraylib -lm (or raylib_headless_shim.c for headless testing)

Public Structure Color
  Dim r As Byte
  Dim g As Byte
  Dim b As Byte
  Dim a As Byte
End Structure

Public Structure Vector2
  Dim x, y As Single
End Structure

Public Structure Vector3
  Dim x, y, z As Single
End Structure

Public Structure Rectangle
  Dim x, y, width, height As Single
End Structure

Public Structure Camera3D
  Dim position, target, up As Vector3
  Dim fovy As Single
  Dim projection As Integer
End Structure

Public Enum CameraProjection
  CAMERA_PERSPECTIVE = 0
  CAMERA_ORTHOGRAPHIC = 1
End Enum

' Core window & timing
Declare Sub InitWindow(width, height As Integer, title As Pointer)
Declare Sub CloseWindow()
Declare Sub SetTargetFPS(fps As Integer)
Declare Function WindowShouldClose() As Boolean
Declare Function GetFrameTime() As Single

' 2D Drawing
Declare Sub BeginDrawing()
Declare Sub EndDrawing()
Declare Sub ClearBackground(color As Color)
Declare Sub DrawCircleV(center As Vector2, radius As Single, color As Color)
Declare Sub DrawRectangleRec(rec As Rectangle, color As Color)
Declare Sub DrawText(text As Pointer, posX, posY, fontSize As Integer, color As Color)

' 3D Camera & Drawing
Declare Sub BeginMode3D(camera As Camera3D)
Declare Sub EndMode3D()
Declare Sub DrawCube(position As Vector3, width, height, length As Single, color As Color)
Declare Sub DrawGrid(slices As Integer, spacing As Single)

' Input
Declare Function GetMousePosition() As Vector2
Declare Function IsKeyDown(key As Integer) As Boolean

Public Function MakeColor(r, g, b, a As Byte) As Color
  Dim c As Color With ( r = r, g = g, b = b, a = a )
  Return c
End Function

Public Function MakeVec3(x, y, z As Single) As Vector3
  Dim v As Vector3 With ( x = x, y = y, z = z )
  Return v
End Function
