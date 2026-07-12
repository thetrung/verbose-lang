
Public Structure Token
    Public Kind As Integer
    Public Value As Pointer
End Structure

Function main () As Integer
  Dim t As Token(1,0x12345678)
  Dim kind As Integer = t.Kind
  kind = -2
  t.Kind = 4
  t.Value = 0x1234
  If kind < t.Kind Then
    printf("  kind = %d (correct=-2)\n", kind)
  End If 
  printf("  t.Kind = %d (correct=4)\n",t.Kind)
  printf("  t.Value = %d (correct=4660)\n", t.Value)
  Return 0
End Function
