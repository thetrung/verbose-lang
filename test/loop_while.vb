Function main() As Integer
  Dim calculation As Integer = 13
  Dim flag As Integer = 0
    ' 3. Loop statements and Comparison operators
    While calculation > flag Do
        calculation = calculation + 1
        flag = flag + 2
        printf("Loop: calculation = %d -- flag = %d\n", calculation, flag)
    End While
  Return 0
End Function
