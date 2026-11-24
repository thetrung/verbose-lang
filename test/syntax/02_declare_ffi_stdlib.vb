Declare Function abs(x As Integer) As Integer
Declare Function strlen(s As Pointer) As Long
Declare Function atoi(s As Pointer) As Integer
Declare Function puts(s As Pointer) As Integer

Public Function main() As Integer
  Dim a As Integer = abs(-42)
  Dim n As Long = strlen("VerBose")
  Dim parsed As Integer = atoi("1234")
  puts("ffi_puts_ok")
  printf("abs=%d strlen=%ld atoi=%d\n", a, n, parsed)
  Return 0
End Function
