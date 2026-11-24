' Declares external C runtime signatures explicitly via FFI Declare
Declare Function malloc(amount As Integer) As Pointer
Declare Sub free(ptr As Pointer)
Declare Function fopen(path As Pointer, mode As Pointer) As Pointer
Declare Function fclose(fp As Pointer) As Integer
Declare Function getc(fp As Pointer) As Integer

Public Function main() As Integer
  ' 1. Memory and I/O initialization via C hooks
  Dim buffer As Pointer = malloc(512)
  Dim fileHandle As Pointer = fopen("src/ast.vb", "r")

  For i = 0 To 100
    Dim charCode As Integer = getc(fileHandle)

    ' 2. Condition blocks testing
    If charCode = 32 Then
      ' Skip spaces
      Printf("#32: Norm.\n")
    Else
      ' Check structure using Select Case
      Select Case charCode
        Case 73, 78
          Dim isOp As Integer = 1
          Printf("#73, #78 => JackPot bro \n")

        Case 114, 111, 112
          Dim isNum As Integer = 1
          Printf("#114, #111, #112 : Nah.\n")

        Case Else
          Printf("%d-%c:%d ", i, charCode, charCode)
      End Select
    End If
  End For

  fclose(fileHandle)
  free(buffer)
  Return 0
End Function
