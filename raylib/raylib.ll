; 1. Define the structural components matching C layout rules
; Single maps to 'float' (32-bit floating point)
%struct.Vector3 = type <{ float, float, float }>
; Vector3:
  ; x float ??
  ; y float ??
  ; z float ??
;
;
; Camera3D aggregates three Vector3 layouts, a float, and an i32 (Total: 44 bytes)
%struct.Camera3D = type <{ %struct.Vector3, %struct.Vector3, %struct.Vector3, float, i32 }>

; 2. Declare the Raylib function prototype accepting the aggregate structure by value
declare void @BeginMode3D(%struct.Camera3D)

define i32 @main() {
entry:
    ; Allocate space for the camera structure on the stack
    %myCamera = alloca %struct.Camera3D, align 4

    ; ... (Your compiler emits GEP and store instructions to populate fields) ...
    ; Example: setting myCamera.FovY (offset index 3 in the main struct layout)
    %fov_ptr = getelementptr inbounds %struct.Camera3D, ptr %myCamera, i32 0, i32 3
    store float 45.0, ptr %fov_ptr, align 4

    ; =========================================================================
    ; THE INTEROP ENGINE: LOAD BY VALUE
    ; =========================================================================
    ; De-reference the entire 44-byte structure block from the stack into an 
    ; LLVM value register. 
    %cam_value = load %struct.Camera3D, ptr %myCamera, align 4

    ; Call the external C library function. 
    ; LLVM inspects the target architecture (x86-64 or Arm64) and automatically 
    ; applies the precise OS ABI constraints to safely route this 44-byte block.
    call void @BeginMode3D(%struct.Camera3D %cam_value)

    ret i32 0
}
