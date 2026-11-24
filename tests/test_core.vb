' Core language feature tests (consolidated from standalone examples).
' Covers Booleans, arithmetic/bitwise ops, Enums, loops, hex/Pointer structs, nested structs, Select Case, and C file I/O.

Declare Function fopen(path As Pointer, mode As Pointer) As Pointer
Declare Function fclose(fp As Pointer) As Integer
Declare Function getc(fp As Pointer) As Integer

Private Enum NodeType
  Literal = 1
  BinaryOp = 2
  FunctionCall = 3
End Enum

Private Structure TokenRec
  Dim Kind As Integer
  Dim Value As Pointer
End Structure

Private Structure Vector3
  Dim x As Single
  Dim y As Double
  Dim z As Long
End Structure

Private Structure MixBox
  Dim vec As Vector3
  Dim node As Integer
End Structure

Private Function ComputeCheck() As Integer
  Dim calculation As Integer = (10 + 20 * 3) / 2 Mod 4
  Dim flag As Integer = (1 Shl 4) Or (2 Shr 1) Xor Not 0
  If flag < calculation Then
    Return calculation
  Else
    Return 0
  End If
End Function

Private Sub UpdateMix(ByRef m As MixBox)
  m.vec = Vector3 With (
    x = 3.5,
    y = 4.25,
    z = 999
  )
  m.node = NodeType.FunctionCall
End Sub

Test "Boolean literals, reassignment, and logical operators"
  Dim logic As Boolean = True
  Assert logic = True
  Assert Not (logic = False)
  logic = False
  Assert logic = False
  Assert Not logic
End Test

Test "Arithmetic precedence, Mod, and bitwise Shl/Shr/Or/Xor/Not"
  Dim calc As Integer = (10 + 20 * 3) / 2 Mod 4
  Assert calc = 3
  Dim flag As Integer = (1 Shl 4) Or (2 Shr 1) Xor Not 0
  Assert flag = -18
  Assert ComputeCheck() = 3
End Test

Test "While and For loops with comparisons"
  Dim calculation As Integer = 13
  Dim flag As Integer = 0
  Dim iters As Integer = 0
  While calculation > flag Do
    calculation = calculation + 1
    flag = flag + 2
    iters = iters + 1
  End While
  Assert iters = 13
  Assert calculation = 26
  Assert flag = 26

  Dim total As Integer = 0
  For i = 1 To 10
    total = total + i
  End For
  Assert total = 55
End Test

Test "Enum values and Select Case multi-value dispatch"
  Dim nt As Integer = NodeType.Literal
  Assert nt = 1
  Assert NodeType.BinaryOp = 2
  Assert NodeType.FunctionCall = 3

  Dim hits As Integer = 0
  For k = 1 To 5
    Select Case k
      Case 1, 3
        hits = hits + 10
      Case 2, 4
        hits = hits + 1
      Case Else
        hits = hits + 100
    End Select
  End For
  Assert hits = 122
End Test

Test "Structure positional ctor, hex literals, and Pointer field"
  Dim t As TokenRec = TokenRec(1, 0x12345678)
  Dim kind As Integer = t.Kind
  Assert kind = 1
  kind = -2
  t.Kind = 4
  t.Value = 0x1234
  Assert kind < t.Kind
  Assert t.Kind = 4
  Assert Not IsNull(t.Value)
End Test

Test "Single/Double/Long structs, nested struct copy isolation, and ByRef With update"
  Dim vec1 As Vector3(1.0, 2.000, 9999999)
  Assert vec1.x = 1.0
  Assert vec1.y = 2.0
  Assert vec1.z = 9999999

  Dim mix As MixBox(vec1, NodeType.Literal)
  Assert mix.node = 1
  Assert mix.vec.z = 9999999

  mix.vec = Vector3(1.0, 1.0, 777)
  Assert mix.vec.z = 777
  Assert vec1.z = 9999999, "nested struct mutation must not alias original vec1"

  UpdateMix(ByRef mix)
  Assert mix.vec.x = 3.5
  Assert mix.vec.y = 4.25
  Assert mix.vec.z = 999
  Assert mix.node = 3
End Test

Test "C FFI file reading via fopen/getc/fclose with Select Case"
  Dim fp As Pointer = fopen("src/ast.vb", "r")
  Assert Not IsNull(fp), "src/ast.vb must open cleanly"
  Dim first As Integer = getc(fp)
  Assert first = 39, "first byte of src/ast.vb is comment quote"
  Dim spaces As Integer = 0
  For i = 1 To 60
    Dim ch As Integer = getc(fp)
    Select Case ch
      Case 32
        spaces = spaces + 1
      Case Else
        spaces = spaces
    End Select
  End For
  Assert spaces > 0
  Assert fclose(fp) = 0
End Test
