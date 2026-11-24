' Explicitly re-declare built-in C functions + add fclose to verify deduplication
Declare Function printf(fmt As Pointer, ...) As Integer
Declare Function malloc(size As Integer) As Pointer
Declare Sub free(p As Pointer)
Declare Function fopen(path As Pointer, mode As Pointer) As Pointer
Declare Function getc(fp As Pointer) As Integer
Declare Function fclose(fp As Pointer) As Integer

Public Function main() As Integer
  Dim buf As Pointer = malloc(64)
  free(buf)
  Dim fp As Pointer = fopen("src/ast.vb", "r")
  Dim first As Integer = getc(fp)
  Dim closed As Integer = fclose(fp)
  printf("first=%d closed=%d\n", first, closed)
  Return 0
End Function
