Sub main()
 ' 1. Memory and I/O initialization via C hooks
    Dim buffer As Pointer = malloc(512)
    Dim fileHandle As Pointer = fopen("lib/ast.ml", "r")
    
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

End Sub

