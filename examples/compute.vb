Function TestComputation() As Integer

    ' 2. Numeric and Bitwise testing
    Dim calculation As Integer = (10 + 20 * 3) / 2 Mod 4
    Dim flag As Integer = (1 Shl 4) Or (2 Shr 1) Xor Not 0

    ' Print text strings and numbers directly to the terminal!
    Printf("Initial Calculation Result: %d\n", calculation)
    Printf("Initial Flag Byte Pattern: %d\n", flag)

    If flag > calculation Then
      Return calculation
    Else
      Return 0
    End If
End Function

Public Function main() As Integer
  Dim result As Integer = TestComputation()
  Printf("result = %d\n", result)
  Return 0
End Function
