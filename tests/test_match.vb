' Pattern Matching (Match ... With / Case ... Then / When / End Match) test suite.

Include "std/strings.vb"
Include "harness.vb"

Private Static g_sideEffect As Integer = 0

Private Sub DoSomething()
  g_sideEffect = g_sideEffect + 10
End Sub

Private Function MatchBoolExample(condition As Boolean) As Integer
  Match condition With
    Case True Then Return 1
    Case False Then
      DoSomething()
      Return 0
  End Match
End Function

Enum TokenTag
  TagNum = 1
  TagStr = 2
  TagSym = 3
End Enum

Private Function ClassifyInt(x, target As Integer, boost As Boolean) As String
  Match x With
    Case 0 Then Return "zero"
    Case target Then Return "hit-var"
    Case 1, 2, 3 When boost Then Return "small-boost"
    Case 1, 2, 3 Then Return "small"
    Case _ When x < 0 Then Return "negative"
    Case _ Then
      Dim big As Integer = x * 2
      Return $"large:{big}"
  End Match
End Function

Private Function ResolveCmd(cmd As String) As Integer
  Match cmd With
    Case 0 Then Return -1
    Case "start", "run" Then Return 1
    Case "stop", "quit" Then Return 2
    Case "status" Then Return 3
    Case _ Then Return 0
  End Match
End Function

Private Function SafeFirstChar(p As Pointer) As Integer
  Match With
    Case IsNull(p) Then Return -1
    Case p(0) = 65 Then Return 1
    Case p(0) = 66 Then Return 2
    Case _ Then Return 0
  End Match
End Function

Private Function MatchFloat(v As Double) As Integer
  Match v With
    Case 0.0 Then Return 0
    Case 1.5, 2.5 Then Return 1
    Case Else Then Return 2
  End Match
End Function

Private Function MatchNested(a, b As Integer) As Integer
  Dim res As Integer = 0
  Match a With
    Case 1 Then
      Match b With
        Case 10 Then res = 110
        Case 20 Then res = 120
        Case _ Then res = 199
      End Match
    Case 2 Then
      res = 200 + b
    Case Else
      res = -1
  End Match
  Return res
End Function

Test "Match Boolean single-line and multi-line Case Then blocks"
  g_sideEffect = 0
  Assert MatchBoolExample(True) = 1
  Assert g_sideEffect = 0
  Assert MatchBoolExample(False) = 0
  Assert g_sideEffect = 10
End Test

Test "Match integer, variable, multi-value, When guards, and wildcard _"
  Assert StrEq(ClassifyInt(0, 99, False), "zero")
  Assert StrEq(ClassifyInt(99, 99, False), "hit-var")
  Assert StrEq(ClassifyInt(2, 99, True), "small-boost")
  Assert StrEq(ClassifyInt(2, 99, False), "small")
  Assert StrEq(ClassifyInt(-5, 99, False), "negative")
  Assert StrEq(ClassifyInt(10, 99, False), "large:20")

  Dim tag As Integer = TokenTag.TagStr
  Dim tagName As String = ""
  Match tag With
    Case TokenTag.TagNum Then tagName = "num"
    Case TokenTag.TagStr Then tagName = "str"
    Case TokenTag.TagSym Then tagName = "sym"
    Case _ Then tagName = "other"
  End Match
  Assert StrEq(tagName, "str")
End Test

Test "Match String/Pointer values, multi-value strings, and null safety"
  Dim nilPtr As String = 0
  Dim dynCmd As String = "st" & "art"
  Dim buf(8) As Byte
  buf(0) = 113
  buf(1) = 117
  buf(2) = 105
  buf(3) = 116
  buf(4) = 0
  Assert ResolveCmd(nilPtr) = -1
  Assert ResolveCmd("start") = 1
  Assert ResolveCmd(dynCmd) = 1
  Assert ResolveCmd("run") = 1
  Assert ResolveCmd(buf) = 2
  Assert ResolveCmd("status") = 3
  Assert ResolveCmd("unknown") = 0
End Test

Test "Match With conditional ladder, short-circuit safety, float, and nested Match"
  Dim nilPtr As Pointer = 0
  Assert SafeFirstChar(nilPtr) = -1
  Assert SafeFirstChar("Alpha") = 1
  Assert SafeFirstChar("Beta") = 2
  Assert SafeFirstChar("Gamma") = 0

  Assert MatchFloat(0.0) = 0
  Assert MatchFloat(1.5) = 1
  Assert MatchFloat(2.5) = 1
  Assert MatchFloat(9.9) = 2

  Assert MatchNested(1, 10) = 110
  Assert MatchNested(1, 20) = 120
  Assert MatchNested(1, 99) = 199
  Assert MatchNested(2, 7) = 207
  Assert MatchNested(9, 0) = -1
End Test

Test "Type Analyzer rejects mismatched Match pattern and non-Boolean When guard"
  Dim tmpSrc(256) As Byte
  Dim pid As Integer = getpid()
  If IsWindowsHost() Then
    snprintf(tmpSrc, 256, "%s\\vb_match_err_%d.vb", GetTempDir(), pid)
  Else
    snprintf(tmpSrc, 256, "%s/vb_match_err_%d.vb", GetTempDir(), pid)
  End If

  WriteTextFile(tmpSrc, "Public Function main() As Integer\n  Dim x As Integer = 1\n  Match x With\n    Case \"oops\" Then Return 1\n  End Match\n  Return 0\nEnd Function\n")
  Dim rc1 As Integer = RunVb(tmpSrc, "--ta --emit-only")
  Assert rc1 <> 0, "expected --ta to reject string pattern against Integer"
  Assert OutHas("Match Case pattern type mismatch")

  WriteTextFile(tmpSrc, "Public Function main() As Integer\n  Dim x As Integer = 1\n  Match x With\n    Case 1 When \"bad\" Then Return 1\n  End Match\n  Return 0\nEnd Function\n")
  Dim rc2 As Integer = RunVb(tmpSrc, "--ta --emit-only")
  unlink(tmpSrc)
  Assert rc2 <> 0, "expected --ta to reject non-Boolean When guard"
  Assert OutHas("Match When guard must be Boolean")
End Test
