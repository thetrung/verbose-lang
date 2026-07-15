# VerBose Language
Just Another Human-Readable Programming Language. inspired by VisualBasic.

### INTRO
Something like VB6.0/NET but will be compiled without GC 😄 
Just my prototype WIP on top of LLVM for quick-play, nothing serious yet :

```VB

Structure Vector
  Dim x As Single
  Dim y As Double
  Dim z As Long
End Structure

Enum NodeType
  Literal = 1
  BinaryOp = 2
  FunctionCall = 3
End Enum

Structure Mix
  Dim vec As Vector
  Dim node As Integer
End Structure

Function Square(number As Long) As Long
  Dim result As Long
  result = number * number
  Return result
End Function

Sub SetVector(ByRef vec1 As Vector)
  Dim temp_vec As Vector(1.0, 2.000, 9999999)
  printf("ByRef: vec1@%lu :\n %f -- %f -- %ld\n", temp_vec, temp_vec.x, temp_vec.y, temp_vec.z)
  vec1 = temp_vec
End Sub

Function main() As Integer
  ' Struct 
  Dim vec1 As Vector
  printf("uninit vec1@%lu :\n %f -- %f -- %ld)\n", vec1, vec1.x, vec1.y, vec1.z)
  ' ByRef
  SetVector(vec1)
  printf("filled vec1@%lu :\n %f -- %f -- %ld)\n\n", vec1, vec1.x, vec1.y, vec1.z)
  ' Enum 
  Dim nodeType As Integer
  nodeType = NodeType.Literal
  printf("nodeType = %d\n", nodeType)
  ' Function ArgTypes
  Dim squared As Long
  squared = Square(vec1.z)
  printf("vec1.z ^ 2 = %ld\n", squared)
  ' Boolean
  Dim logic As Boolean
  logic = True Xor False
  printf("logic = %d\n", logic)
  ' Nested Struct & Pointer 
  Dim mix As Mix(vec1, 1)
  mix.node = NodeType.Literal
  printf("mix:\n .vec = @%lu\n", mix.vec)
  printf("    .x single %f\n", mix.vec.x)
  printf("    .y double %f\n", mix.vec.y)
  printf("    .z long   %lld\n", mix.vec.z)
  printf(" .node integer %d\n",mix.node)
  Return 0
End Function

```

### USAGE
- It require LLVM@19+ to be installed.

- Build compiler : `make`

- To compile `.vb` into `LLVM` : `main.exe demo.vb`

- Then compile to native code by pipeline :

      main.exe demo.vb && llc demo.ll && clang demo.s -o demo && ./demo

### GOAL 
- Bootstap minimally.
- VB syntax compatible.
- Can have fun coding again.
- Whole language fit into a single page.
- Design for human readability, not for efficiency.
- Easy to read, use, compile, fast as C/Rust, C interop natively.
- Drop GC, Class/Inherit, Complex stuff.. for Arena, Struct/Enum, Async/Await.
- Can work natively with 32-bit ARM microcontroller too, beside any LLVM-supported platform. ( violatile, fixed/deterministic memory management ).
- It may still compile some VB6/NET source code but won't be exactly or fully compatible with them (Ex: become case-sensitive to drop the need for Alias DLL import ).
  

```
