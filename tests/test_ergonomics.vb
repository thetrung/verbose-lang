' Syntax & Semantic Compression test suite:
' - Unqualified Enum member fallback (with local shadowing + ambiguity detection)
' - With <lvalue> ... End With blocks (zero-copy reads/writes, nested, array elements, ByRef/AddressOf)
' - Optional Do on While loops
' - Select Case unified into Match ... With

Include "std/strings.vb"
Include "harness.vb"

Public Enum FruitKind
  FrApple = 10
  FrBanana = 20
  FrCherry = 30
End Enum

Public Enum DupEnumA
  SharedVal = 1
  OnlyA = 100
End Enum

Public Enum DupEnumB
  SharedVal = 2
  OnlyB = 200
End Enum

Public Structure InnerBox
  Dim weight, flags As Integer
End Structure

Public Structure ItemNode
  Dim kind, count As Integer
  Dim box As InnerBox
  Dim scores(4) As Integer
End Structure

Static g_defaultFruit As Integer = FrBanana
Static g_items(3) As ItemNode

Private Sub BumpInt(ByRef v As Integer, d As Integer)
  v = v + d
End Sub

Test "Unqualified Enum member fallback, static const init, Match, and local shadowing"
  Assert g_defaultFruit = 20
  Assert FrApple = 10
  Assert FrCherry = 30
  Assert OnlyA = 100
  Assert OnlyB = 200
  Assert DupEnumA.SharedVal = 1
  Assert DupEnumB.SharedVal = 2

  Dim f As Integer = FrCherry
  Dim label As String = ""
  Match f With
    Case FrApple Then label = "apple"
    Case FrBanana Then label = "banana"
    Case FrCherry Then label = "cherry"
    Case _ Then label = "other"
  End Match
  Assert StrEq(label, "cherry")

  ' Local variable shadows unqualified enum member cleanly
  Dim FrApple As Integer = 999
  Assert FrApple = 999
  Assert FruitKind.FrApple = 10
End Test

Test "With ... End With zero-copy reads, writes, nested structs, arrays, ByRef, and AddressOf"
  Dim item As ItemNode
  With item
    .kind = FrApple
    .count = 5
    .scores(0) = 11
    .scores(1) = 22
    With .box
      .weight = 75
      .flags = 3
    End With
    BumpInt(ByRef .count, 7)
    Dim pW As Pointer = AddressOf(.box.weight)
    PtrWrite(pW, Integer, 88)
  End With

  Assert item.kind = 10
  Assert item.count = 12
  Assert item.scores(0) = 11
  Assert item.scores(1) = 22
  Assert item.box.weight = 88
  Assert item.box.flags = 3

  ' With on array element lvalue (evaluated once, mutates in-place)
  Dim idx As Integer = 1
  With g_items(idx)
    .kind = FrBanana
    .count = 42
    .box.weight = .count + 8
  End With
  Assert g_items(1).kind = 20
  Assert g_items(1).count = 42
  Assert g_items(1).box.weight = 50
End Test

Test "Enforced For ... Do / While ... Do and Select Case unified into Match ... With"
  Dim s1 As Integer = 0
  For i = 0 To 4 Do
    s1 = s1 + i
  End For
  Assert s1 = 10

  Dim s2 As Integer = 0
  Dim j As Integer = 0
  While j < 5 Do
    s2 = s2 + j
    j = j + 1
  End While
  Assert s2 = 10

  ' Select Case desugared to NdMatch (supports enums, multi-value, Case Else, strings)
  Dim res As Integer = 0
  Select Case g_defaultFruit
    Case FrApple
      res = 1
    Case FrBanana, FrCherry
      res = 2
    Case Else
      res = 9
  End Select
  Assert res = 2

  Dim cmd As String = "go"
  Dim code As Integer = 0
  Select Case cmd
    Case "stop"
      code = 1
    Case "go", "run"
      code = 2
    Case Else
      code = 3
  End Select
  Assert code = 2
End Test

Test "Type Analyzer rejects ambiguous unqualified enum, non-struct With, and removed ByVal"
  Dim tmpSrc(256) As Byte
  Dim pid As Integer = getpid()
  If IsWindowsHost() Then
    snprintf(tmpSrc, 256, "%s\\vb_ergo_err_%d.vb", GetTempDir(), pid)
  Else
    snprintf(tmpSrc, 256, "%s/vb_ergo_err_%d.vb", GetTempDir(), pid)
  End If

  ' 1. Ambiguous unqualified enum member
  WriteTextFile(tmpSrc, "Enum A\n  Dup = 1\nEnd Enum\nEnum B\n  Dup = 2\nEnd Enum\nPublic Function main() As Integer\n  Dim x As Integer = Dup\n  Return x\nEnd Function\n")
  Dim rc1 As Integer = RunVb(tmpSrc, "--ta --emit-only")
  Assert rc1 <> 0, "expected ambiguous enum member error"
  Assert OutHas("Ambiguous enum member")

  ' 2. With on non-struct target
  WriteTextFile(tmpSrc, "Public Function main() As Integer\n  Dim x As Integer = 5\n  With x\n    x = 6\n  End With\n  Return 0\nEnd Function\n")
  Dim rc2 As Integer = RunVb(tmpSrc, "--ta --emit-only")
  Assert rc2 <> 0, "expected non-struct With error"
  Assert OutHas("With target must be a Structure")

  ' 3. Removed ByVal keyword rejected by parser
  WriteTextFile(tmpSrc, "Sub Foo(ByVal x As Integer)\nEnd Sub\nPublic Function main() As Integer\n  Return 0\nEnd Function\n")
  Dim rc3 As Integer = RunVb(tmpSrc, "--emit-only")
  Assert rc3 <> 0, "expected removed ByVal keyword to fail parse"

  ' 4. Missing Do on While and For rejected by parser
  WriteTextFile(tmpSrc, "Public Function main() As Integer\n  Dim i As Integer = 0\n  While i < 3\n    i = i + 1\n  End While\n  Return 0\nEnd Function\n")
  Dim rc4 As Integer = RunVb(tmpSrc, "--emit-only")
  Assert rc4 <> 0, "expected While without Do to fail parse"

  WriteTextFile(tmpSrc, "Public Function main() As Integer\n  For i = 0 To 3\n  End For\n  Return 0\nEnd Function\n")
  Dim rc5 As Integer = RunVb(tmpSrc, "--emit-only")
  unlink(tmpSrc)
  Assert rc5 <> 0, "expected For without Do to fail parse"
End Test
