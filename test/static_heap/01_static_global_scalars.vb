Public Static pub_counter As Integer = 10
Private Static priv_counter As Integer = 5
Static g_label As Pointer = "VB"
Static g_flag As Boolean = True

Private Sub BumpAll()
  pub_counter = pub_counter + 3
  priv_counter = priv_counter + 2
  g_flag = False
End Sub

Public Function main() As Integer
  BumpAll()
  printf("pub=%d priv=%d label=%s flag=%d\n", pub_counter, priv_counter, g_label, g_flag)
  Return 0
End Function
