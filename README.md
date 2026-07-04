# VerBose Language
Just Another Human-Readable Programming Language. inspired by VisualBasic.

### INTRO
Something like VB6.0/NET but will be compiled without GC 😄 
Just my prototype WIP on top of LLVM for quick-play, nothing serious yet :

```VB

Public Structure Token
    Public Kind As Integer
    Public Value As Pointer
End Structure

Function CompileKernel() As Integer
    ' 1. Memory and I/O initialization via C hooks
    Dim buffer As Pointer = malloc(512)
    Dim fileHandle As Pointer = fopen("lib/ast.ml", "r")
    
    ' 2. Numeric and Bitwise testing
    Dim calculation As Integer = (10 + 20 * 3) / 2 Mod 4
    Dim flag As Integer = (1 Shl 4) Or (2 Shr 1) Xor Not 0

    ' Print text strings and numbers directly to the terminal!
    printf("--- VERBOSE LANGUAGE RUNTIME ---\n")
    printf("Initial Calculation Result: %d\n", calculation)
    printf("Initial Flag Byte Pattern: %d\n", flag)

    ' 3. Loop statements and Comparison operators
    While calculation > flag Do
        calculation = calculation + 1
        flag = flag + 2
        printf("Loop: calculation = %d -- flag = %d\n", calculation, flag)
    End While
   
    For i = 0 To 100
      Dim charCode As Integer = getc(fileHandle)

        ' 4. Condition blocks testing
        If charCode = 32 Then
            ' Skip spaces
            printf("#32: Norm.\n")
        Else
            ' Check structure using Select Case
            Select Case charCode
                Case 73, 78
                    Dim isOp As Integer = 1
                    printf("#73, #78 => JackPot bro \n")
                Case 114, 111, 112
                    Dim isNum As Integer = 1
                    printf("#114, #111, #112 : Nah.\n")
                Case Else
                    printf("%d-%c:%d ", i, charCode, charCode)
            End Select
        End If

    End For

    free(buffer)

    If flag > calculation Then
      Return calculation
    Else
      Return 0
    End If
End Function

Function main () As Integer
  Dim result As Integer = CompileKernel()
  Return result
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
  
### STATE
```
We are building "verbose-lang", a VB.NET/VB6-inspired systems programming language compiled to LLVM IR via an OCaml text-patching string generator backend (no native LLVM bindings dependency, targeting Apple Silicon/arm64 macOS compatibility). 

Here is our current state:
1. Lexer/Parser: Built via ocamllex and Menhir. Supports conditional blocks, while loops, for loops (fully evaluated bodies), multi-variable Select Case matching, and Case Else fallbacks. Resolves shift/reduce conflicts via %nonassoc ID and an explicit recursive case_branches rule structure.
2. AST: Single source of truth in ast.ml tracking primitive data types, custom StructType(name).
3. Codegen: Emits raw string-concatenated LLVM assembly text with modern opaque pointers (ptr). Handles proper 8-byte boundaries for pointers, dynamically records global string constants (e.g. for printf and file paths like fopen), manages unique basic block label counters, and calculates direct struct field memory byte offsets via getelementptr i8. It compiles cleanly with llc + clang and executes with 0 segmentation faults.

Our next goals are to look into implementing FieldAccess + FieldAssign DOT notation, Enums, Arrays, Raylib-compatile, built-in Arena Allocator, Type Checking/Semantic Analysis... and finally, map out a mini compiler layout in VerBose syntax to begin bootstrapping the language.
```
