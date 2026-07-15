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
  Dim result As Long = number * number
  Return result
End Function

Function main() As Integer
  Dim vec1 As Vector(1.0, 2.000, 9999999)
  printf("vec1@%lu :\n %f -- %f -- %ld)\n", vec1, vec1.x, vec1.y, vec1.z)
  
  Dim nodeType As Integer = NodeType.Literal
  printf("nodeType = %d\n", nodeType)

  Dim squared As Long = Square(vec1.z)
  printf("vec1.z ^ 2 = %ld\n", squared)

  Dim mix As Mix(vec1, 1)
  ' mix.vec = Vector(1.0, 1.0, 999)
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
