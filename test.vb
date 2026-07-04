
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
