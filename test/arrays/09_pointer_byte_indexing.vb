Public Function main() As Integer
  Dim buf As Pointer = malloc(8)
  buf(0) = 86
  buf(1) = 66
  buf(2) = 0
  Dim msg As Pointer = "XYZ"
  printf("buf=%s msg0=%c msg2=%c\n", buf, msg(0), msg(2))
  free(buf)
  Return 0
End Function
