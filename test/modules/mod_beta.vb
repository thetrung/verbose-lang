Private Static secret_factor As Integer = 10

Private Function helper(x As Integer) As Integer
  Return x * secret_factor
End Function

Public Function ComputeBeta(x As Integer) As Integer
  Return helper(x) + 2
End Function
