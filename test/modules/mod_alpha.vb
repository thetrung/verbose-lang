Private Static secret_factor As Integer = 3

Private Function helper(x As Integer) As Integer
  Return x * secret_factor
End Function

Public Function ComputeAlpha(x As Integer) As Integer
  Return helper(x) + 1
End Function
