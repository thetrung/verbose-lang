Include "sub/mod_a.vb"
Include "sub/mod_b.vb"
Include "common/shared.vb"

Public Function main() As Integer
  Dim a As Integer = FromModA()
  Dim b As Integer = FromModB()
  Printf("a=%d b=%d shared=%d\n", a, b, g_shared_counter)
  Return 0
End Function
