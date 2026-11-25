' VerBose-Lang Test Suite: Break & Continue control flow

Include "harness.vb"

Test "Break in While loop"
  Dim i As Integer = 0
  Dim sum As Integer = 0
  While i < 100 Do
    If i = 5 Then
      Break
    End If
    sum = sum + i
    i = i + 1
  End While
  Assert i = 5, "While loop should stop at i = 5"
  Assert sum = 10, "Sum of 0..4 should be 10"
End Test

Test "Continue in While loop"
  Dim i As Integer = 0
  Dim sum As Integer = 0
  While i < 10 Do
    i = i + 1
    If (i Mod 2) = 0 Then
      Continue
    End If
    sum = sum + i
  End While
  Assert i = 10, "While loop should reach i = 10"
  Assert sum = 25, "Sum of odd numbers 1..9 should be 25"
End Test

Test "Break in For loop"
  Dim sum As Integer = 0
  Dim last As Integer = -1
  For i = 1 To 50 Do
    If i = 6 Then
      Break
    End If
    sum = sum + i
    last = i
  End For
  Assert last = 5, "Last processed iteration should be 5"
  Assert sum = 15, "Sum of 1..5 should be 15"
End Test

Test "Continue in For loop increments counter"
  Dim sum As Integer = 0
  For i = 1 To 10 Do
    If (i = 3) Or (i = 7) Then
      Continue
    End If
    sum = sum + i
  End For
  Assert sum = 45, "Sum of 1..10 skipping 3 and 7 should be 45"
End Test

Test "Nested loops Break and Continue target innermost loop"
  Dim total As Integer = 0
  For outer = 1 To 4 Do
    If outer = 3 Then
      Continue
    End If
    Dim inner As Integer = 0
    While inner < 10 Do
      inner = inner + 1
      If inner = 2 Then
        Continue
      End If
      If inner = 5 Then
        Break
      End If
      total = total + outer * 10 + inner
    End While
  End For
  ' outer in {1, 2, 4}, inner in {1, 3, 4}:
  ' outer=1: 11+13+14 = 38
  ' outer=2: 21+23+24 = 68
  ' outer=4: 41+43+44 = 128
  ' total = 234
  Assert total = 234, "Nested Break/Continue sum should be 234"
End Test

Test "Break and Continue inside Match With"
  Dim acc As Integer = 0
  For i = 0 To 9 Do
    Match i With
      Case 2, 4 Then
        Continue
      Case 7 Then
        Break
      Case _ Then
        acc = acc + i
    End Match
  End For
  ' 0 + 1 + 3 + 5 + 6 = 15
  Assert acc = 15, "Match-driven Break/Continue sum should be 15"
End Test

Test "Type Analyzer rejects Break and Continue outside loops"
  Dim p1(256), p2(256) As Byte
  Dim pid As Integer = getpid()
  snprintf(p1, 256, "%s/vb_bad_break_%d.vb", GetTempDir(), pid)
  snprintf(p2, 256, "%s/vb_bad_cont_%d.vb", GetTempDir(), pid)

  Assert WriteTextFile(p1, "Public Function main() As Integer\n  Break\n  Return 0\nEnd Function\n"), "write bad break"
  Dim rc1 As Integer = RunVb(p1, "")
  unlink(p1)
  Assert rc1 <> 0, "Break outside loop should fail"
  Assert OutHas("'Break' can only be used inside"), "expected Break error diagnostic"

  Assert WriteTextFile(p2, "Public Function main() As Integer\n  Continue\n  Return 0\nEnd Function\n"), "write bad continue"
  Dim rc2 As Integer = RunVb(p2, "")
  unlink(p2)
  Assert rc2 <> 0, "Continue outside loop should fail"
  Assert OutHas("'Continue' can only be used inside"), "expected Continue error diagnostic"
End Test
