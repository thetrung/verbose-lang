Include "mod_alpha.vb"
Include "mod_beta.vb"

Private Function helper(x As Integer) As Integer
  Return x + 100
End Function

Public Function main() As Integer
  printf("alpha=%d beta=%d local=%d\n", ComputeAlpha(4), ComputeBeta(4), helper(4))
  Return 0
End Function
