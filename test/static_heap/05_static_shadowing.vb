Static g_num As Integer = 50

Private Function ShadowRead() As Integer
  Dim g_num As Integer = 7
  g_num = g_num + 1
  Return g_num
End Function

Public Function main() As Integer
  Dim localVal As Integer = ShadowRead()
  printf("local=%d global=%d\n", localVal, g_num)
  Return 0
End Function
