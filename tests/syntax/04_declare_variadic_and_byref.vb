Declare Function sscanf(src As Pointer, fmt As Pointer, ...) As Integer
Declare Function snprintf(buf As Pointer, size As Long, fmt As Pointer, ...) As Integer

Public Function main() As Integer
  Dim a As Integer = 0
  Dim b As Integer = 0
  Dim matched As Integer = sscanf("42 99", "%d %d", ByRef a, ByRef b)

  Dim buf As Pointer = Malloc(128)
  Dim cap As Long = 128
  Dim f As Single = 3.5
  Dim written As Integer = snprintf(buf, cap, "a=%d b=%d f=%.1f", a, b, f)
  Printf("matched=%d written=%d text=[%s]\n", matched, written, buf)
  Free(buf)
  Return 0
End Function
