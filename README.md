# VerBose Language
Just Another Human-Readable Programming Language. inspired by VisualBasic.

### INTRO
Something like VB6.0/NET but will be compiled without GC 😄 
Just my prototype WIP on top of LLVM for quick-play, nothing serious yet :

```VB
' This is a native code comment.

' Example Struct :
Public Structure Player
  Public ID As Integer
  Public FixedName(31) As Byte
End Structure
  
' Function that modify Structure 
Function UpdatePlayer(p As Player) As Integer
  p.ID = 777
  Return p.ID
End Function

' Function Call, field access, Printf..
Function InitializeMainEngine() As Integer
  Dim localPlayer As Player = New
  localPlayer.ID = 100
  UpdatePlayer (localPlayer)
  Printf ("Updated ID: {localPlayer.ID}")
  Return localPlayer.ID
End Function

' EntryPoint 
Function Main () As Nothing
  Dim result As Integer = InitializeMainEngine()
  Printf ("Result: {result}")
End Function
```
### GOAL 
- Can have fun coding again.
- Whole language fit into a single page.
- Design for human readability, not for efficiency.
- Easy to read, use, compile, fast as C/Rust, C interop natively.
- Drop GC, Class/Inherit, Complex stuff.. for Arena, Struct, Async/Await.
- Can work natively with 32-bit ARM microcontroller too, beside any LLVM-supported platform. ( violatile, fixed/deterministic memory management ).
- It may still compile some VB6/NET source code but won't be exactly or fully compatible with them (Ex: become case-sensitive to drop the need for Alias DLL import ).
