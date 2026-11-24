' Raylib 5.x FFI Bindings for VerBose-Lang
' Usage: Include "raylib.vb" and link with -lraylib -lm (or raylib_headless_shim.c for headless testing)

Public Structure Color
  Dim r As Byte
  Dim g As Byte
  Dim b As Byte
  Dim a As Byte
End Structure

Public Structure Vector2
  Dim x As Single
  Dim y As Single
End Structure

Public Structure Rectangle
  Dim x As Single
  Dim y As Single
  Dim width As Single
  Dim height As Single
End Structure

' Core window & timing
Declare Sub InitWindow(width As Integer, height As Integer, title As Pointer)
Declare Sub CloseWindow()
Declare Sub SetTargetFPS(fps As Integer)
Declare Function WindowShouldClose() As Boolean
Declare Function GetFrameTime() As Single

' Drawing
Declare Sub BeginDrawing()
Declare Sub EndDrawing()
Declare Sub ClearBackground(color As Color)
Declare Sub DrawCircleV(center As Vector2, radius As Single, color As Color)
Declare Sub DrawRectangleRec(rec As Rectangle, color As Color)
Declare Sub DrawText(text As Pointer, posX As Integer, posY As Integer, fontSize As Integer, color As Color)

' Input
Declare Function GetMousePosition() As Vector2
Declare Function IsKeyDown(key As Integer) As Boolean

Public Function MakeColor(r As Byte, g As Byte, b As Byte, a As Byte) As Color
  Dim c As Color With ( r = r, g = g, b = b, a = a )
  Return c
End Function
