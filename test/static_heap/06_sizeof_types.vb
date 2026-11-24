Structure Padded
  Dim a As Byte
  Dim b As Long
End Structure

Public Function main() As Integer
  printf("b=%d s=%d i=%d l=%d f=%d d=%d p=%d st=%d\n", SizeOf(Byte), SizeOf(Short), SizeOf(Integer), SizeOf(Long), SizeOf(Single), SizeOf(Double), SizeOf(Pointer), SizeOf(Padded))
  Return 0
End Function
