; Declare external C library printf function
declare i32 @printf(ptr, ...)

; Allocate global format string constant for printf
@.str.fmt = private unnamed_addr constant [24 x i8] c"Player ID Modified: %d\0A\00", align 1

%struct.player = type { i32, [32 x i8] }

define i32 @updateplayer(ptr %p) {
entry:
    %1 = getelementptr inbounds %struct.player, ptr %p, i32 0, i32 0
    store i32 777, ptr %1, align 4
    %2 = getelementptr inbounds %struct.player, ptr %p, i32 0, i32 0
    %3 = load i32, ptr %2, align 4
    ret i32 %3
}

define i32 @initializemainengine() {
entry:
    %localplayer.alloc = alloca %struct.player, align 4
    %1 = getelementptr inbounds %struct.player, ptr %localplayer.alloc, i32 0, i32 0
    store i32 100, ptr %1, align 4
    %result.alloc = alloca i32, align 4
    store i32 0, ptr %result.alloc, align 4
    %2 = getelementptr inbounds %struct.player, ptr %localplayer.alloc, i32 0, i32 0
    %3 = load i32, ptr %2, align 4
    store i32 %3, ptr %result.alloc, align 4
    %4 = load i32, ptr %result.alloc, align 4
    ret i32 %4
}

define i32 @main() {
entry:
    ; Allocate our Player structure on the execution stack frame
    %localPlayer = alloca %struct.player, align 4
    
    ; Safely seed initial struct properties via structural base offsets
    %id_ptr = getelementptr inbounds %struct.player, ptr %localPlayer, i32 0, i32 0
    store i32 100, ptr %id_ptr, align 4
    
    ; Call updateplayer, passing our stack address as a pointer (ByRef)
    %updated_id = call i32 @updateplayer(ptr %localPlayer)
    
    ; Printf out the execution updates directly to stdout terminal window
    %print_call = call i32 (ptr, ...) @printf(ptr @.str.fmt, i32 %updated_id)
    
    ; Return 0 exit status payload
    ret i32 0
}
