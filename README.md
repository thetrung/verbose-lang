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
    ' 3. Loop statements and Comparison operators
    While calculation > flag Do
        calculation = calculation + 1
        flag = flag + 2
    End While
   
    For i = 0 To 100
      Dim charCode As Integer = getc(fileHandle)

        ' 4. Condition blocks testing
        If charCode = 32 Then
            ' Skip spaces
        Else
            ' Check structure using Select Case
            Select Case charCode
                Case 43, 45
                    Dim isOp As Integer = 1
                Case 48, 49, 50
                    Dim isNum As Integer = 1
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
### GOAL 
- Can have fun coding again.
- Whole language fit into a single page.
- Design for human readability, not for efficiency.
- Easy to read, use, compile, fast as C/Rust, C interop natively.
- Drop GC, Class/Inherit, Complex stuff.. for Arena, Struct, Async/Await.
- Can work natively with 32-bit ARM microcontroller too, beside any LLVM-supported platform. ( violatile, fixed/deterministic memory management ).
- It may still compile some VB6/NET source code but won't be exactly or fully compatible with them (Ex: become case-sensitive to drop the need for Alias DLL import ).
