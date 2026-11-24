' --- 3D MATH STRUCTURES ---
Structure Vector3 Layout(Packed)
    Dim X As Single ' 4-byte Float
    Dim Y As Single 
    Dim Z As Single
End Structure

Structure Camera3D Layout(Packed)
    Dim Position   As Vector3 ' 12 bytes
    Dim Target     As Vector3 ' 12 bytes
    Dim Up         As Vector3 ' 12 bytes
    Dim FovY       As Single  ' 4 bytes
    Dim Projection As Int32   ' 4 bytes (Enum matching CAMERA_PERSPECTIVE)
End Structure

' --- RAYLIB NATIVE IMPORT ---
' Passing the whole Camera3D struct natively ByVal
Declare Sub BeginMode3D Lib "raylib" (ByVal cam As Camera3D)

Function Main() As Int32
    ' 1. Allocate and fill the structure sequentially
    Dim myCamera As Camera3D
    
    myCamera.Position.X = 0.0
    myCamera.Position.Y = 10.0
    myCamera.Position.Z = 10.0
    
    myCamera.Target.X   = 0.0
    myCamera.Target.Y   = 0.0
    myCamera.Target.Z   = 0.0
    
    myCamera.Up.Y       = 1.0 ' Up vector pointing straight up
    myCamera.FovY       = 45.0
    myCamera.Projection = 0    ' CAMERA_PERSPECTIVE

    ' 2. Pass the entire structure seamlessly by value
    Call BeginMode3D(myCamera)

    Return 0
End Function
