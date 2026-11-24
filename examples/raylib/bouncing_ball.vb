' Raylib Bouncing Ball + Trail Demo written in VerBose-Lang
' Build with real Raylib:
'   ./verbose examples/raylib/bouncing_ball.vb -lraylib -lm
' Build & run headless verification shim:
'   ./verbose examples/raylib/bouncing_ball.vb examples/raylib/raylib_headless_shim.c -lm

Include "raylib.vb"

Structure Ball
  Dim pos As Vector2
  Dim vel As Vector2
  Dim radius As Single
End Structure

Private Sub StepBall(ByRef b As Ball, dt As Single, maxW As Single, maxH As Single)
  b.pos.x = b.pos.x + b.vel.x * dt
  b.pos.y = b.pos.y + b.vel.y * dt

  Dim minX As Single = b.radius
  Dim limitX As Single = maxW - b.radius
  If b.pos.x < minX Then
    b.pos.x = minX
    b.vel.x = -b.vel.x
  End If
  If b.pos.x > limitX Then
    b.pos.x = limitX
    b.vel.x = -b.vel.x
  End If

  Dim minY As Single = b.radius
  Dim limitY As Single = maxH - b.radius
  If b.pos.y < minY Then
    b.pos.y = minY
    b.vel.y = -b.vel.y
  End If
  If b.pos.y > limitY Then
    b.pos.y = limitY
    b.vel.y = -b.vel.y
  End If
End Sub

Public Function main() As Integer
  Dim width As Integer = 800
  Dim height As Integer = 450
  InitWindow(width, height, "VerBose + Raylib Demo")
  SetTargetFPS(60)

  Dim bg As Color = MakeColor(24, 24, 36, 255)
  Dim ballColor As Color = MakeColor(230, 41, 55, 255)
  Dim padColor As Color = MakeColor(80, 200, 120, 255)
  Dim textColor As Color = MakeColor(240, 240, 240, 255)

  Dim ball As Ball With {
    pos = Vector2 With { x = 760.0, y = 200.0 },
    vel = Vector2 With { x = 120.0, y = 60.0 },
    radius = 20.0
  }

  Dim pad As Rectangle With { x = 350.0, y = 410.0, width = 100.0, height = 16.0 }

  ' Scratch arena reset every frame (zero heap churn in game loop!)
  Dim frameArena As Arena = ArenaCreate(1024)

  While Not WindowShouldClose() Do
    ArenaReset(ByRef frameArena)
    Dim dt As Single = GetFrameTime()
    StepBall(ByRef ball, dt, 800.0, 450.0)

    Dim mouse As Vector2 = GetMousePosition()
    pad.x = mouse.x - 50.0

    ' Allocate per-frame trail snapshot in scratch arena
    Dim trail[] As Vector2 = ArenaAlloc(ByRef frameArena, Vector2, 1)
    trail[0] = ball.pos

    BeginDrawing()
    ClearBackground(bg)
    DrawRectangleRec(pad, padColor)
    DrawCircleV(trail[0], ball.radius, ballColor)
    DrawText("VerBose-Lang + Raylib", 16, 16, 20, textColor)
    EndDrawing()
  End While

  ArenaDestroy(ByRef frameArena)
  CloseWindow()
  Return 0
End Function
