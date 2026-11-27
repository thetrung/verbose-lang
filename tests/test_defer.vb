' VerBose Test Suite: Milestone 3 — Deterministic Defer Statement

Include "harness.vb"
Include "../std/result.vb"

Private Static g_log(64) As Integer
Private Static g_logLen As Integer = 0

Private Sub ResetLog()
  g_logLen = 0
End Sub

Private Sub PushLog(v As Integer)
  If g_logLen < 64 Then
    g_log(g_logLen) = v
    g_logLen = g_logLen + 1
  End If
End Sub

Structure DeferPair
  Dim a, b As Integer
End Structure

Private Sub RunLifoAndBlocks(flag As Boolean)
  PushLog(1)
  Defer PushLog(100)
  Defer
    PushLog(90)
    PushLog(91)
  End Defer
  If flag Then
    PushLog(2)
    Defer PushLog(20)
    Defer PushLog(21)
    PushLog(3)
  Else
    Defer PushLog(30)
  End If
  PushLog(4)
End Sub

Private Function ReturnValueCapturedBeforeDefer(ByRef outFlag As Integer) As Integer
  Dim x As Integer = 42
  Defer
    outFlag = x + 1
    x = 999
  End Defer
  Return x
End Function

Private Function ReturnStructCapturedBeforeDefer() As DeferPair
  Dim p As DeferPair = DeferPair With ( a = 7, b = 11 )
  Defer
    p.a = -1
    p.b = -2
  End Defer
  Return p
End Function

Private Function EarlyReturnUnwindsAll(mode As Integer) As Integer
  Defer PushLog(1)
  If mode = 1 Then
    Defer PushLog(2)
    Return 10
  End If
  Defer PushLog(3)
  If mode = 2 Then
    Defer PushLog(4)
    Return 20
  End If
  Return 30
End Function

Private Sub LoopsBreakContinue()
  Defer PushLog(999)
  For i = 1 To 4 Do
    Defer PushLog(i * 10)
    If i = 2 Then
      Defer PushLog(22)
      Continue
    End If
    If i = 3 Then
      Defer PushLog(33)
      Break
    End If
    PushLog(i)
  End For

  Dim w As Integer = 0
  While w < 3 Do
    w = w + 1
    Defer PushLog(100 + w)
    If w = 1 Then
      Continue
    End If
    If w = 2 Then
      Break
    End If
  End While
End Sub

Private Sub NestedDeferAndInnerLoop()
  Defer
    PushLog(10)
    Defer PushLog(12)
    For k = 1 To 3 Do
      If k = 2 Then
        Continue
      End If
      If k = 3 Then
        Break
      End If
      PushLog(11)
    End For
  End Defer
  PushLog(1)
End Sub

Private Sub WithAndUnionMatchDefer(opt As Option(Of Integer))
  Dim p As DeferPair = DeferPair With ( a = 1, b = 2 )
  With p
    Defer .a = .a + 10
    .b = 20
  End With
  PushLog(p.a)
  PushLog(p.b)

  Match opt With
    Case Some(v) Then
      Defer PushLog(v * 10)
      PushLog(v)
    Case None Then
      Defer PushLog(-10)
      PushLog(-1)
  End Match
End Sub

Private Sub GotoUnwindsInnerDefer(jump As Boolean)
  Defer PushLog(100)
  If jump Then
    Defer PushLog(50)
    GoTo done
  End If
  PushLog(10)
  Label done
  PushLog(20)
End Sub

Private Function GenericDeferAdd(Of T)(a As T, b As T) As T
  Dim acc As T = a + b
  Defer PushLog(Integer(acc))
  Return acc
End Function

Private Async Function AsyncDeferWork(n As Integer) As Integer
  Dim buf As Pointer = HeapAllocBytes(64)
  Defer HeapFree(buf)
  Return n * 3
End Function

Private Function ArenaAndHeapCleanup(n As Integer) As Integer
  Dim a As Arena = ArenaCreate(1024)
  Defer ArenaDestroy(ByRef a)
  Dim raw As Pointer = HeapAllocBytes(128)
  Defer HeapFree(raw)
  Dim arr() As Integer = ArenaAlloc(ByRef a, Integer, 8)
  arr(0) = n
  arr(1) = n + 5
  Return arr(0) + arr(1)
End Function

Test "LIFO ordering across inline, block, and nested If/Else scopes"
  ResetLog()
  RunLifoAndBlocks(True)
  ' Expected: 1, 2, 3, (If defers LIFO: 21, 20), 4, (fn defers LIFO: 90, 91, 100)
  Assert g_logLen = 9, "expected 9 log entries"
  Assert g_log(0) = 1, "log 0"
  Assert g_log(1) = 2, "log 1"
  Assert g_log(2) = 3, "log 2"
  Assert g_log(3) = 21, "If defer 2"
  Assert g_log(4) = 20, "If defer 1"
  Assert g_log(5) = 4, "after If"
  Assert g_log(6) = 90, "block defer stmt 1"
  Assert g_log(7) = 91, "block defer stmt 2"
  Assert g_log(8) = 100, "outer inline defer"
End Test

Test "Return evaluates scalar & struct before running defers, and early Return unwinds all active scopes"
  Dim flag As Integer = 0
  Dim r As Integer = ReturnValueCapturedBeforeDefer(ByRef flag)
  Assert r = 42, "scalar Return evaluated before Defer mutates local"
  Assert flag = 43, "Defer still ran and saw original value"

  Dim rp As DeferPair = ReturnStructCapturedBeforeDefer()
  Assert (rp.a = 7) And (rp.b = 11), "struct Return loaded before Defer mutates local"

  ResetLog()
  Assert EarlyReturnUnwindsAll(1) = 10, "mode 1 return"
  Assert (g_logLen = 2) And (g_log(0) = 2) And (g_log(1) = 1), "mode 1 unwinds only registered defers in LIFO"

  ResetLog()
  Assert EarlyReturnUnwindsAll(2) = 20, "mode 2 return"
  Assert (g_logLen = 3) And (g_log(0) = 4) And (g_log(1) = 3) And (g_log(2) = 1), "mode 2 unwinds 4, 3, 1"
End Test

Test "While/For Break & Continue, nested Defer, With, Union Match, GoTo, Generics, Async, and Arena/Heap cleanup"
  ResetLog()
  LoopsBreakContinue()
  ' For: i=1 -> push 1, defer 10; i=2 -> Continue -> defer 22, 20; i=3 -> Break -> defer 33, 30
  ' While: w=1 -> Continue -> defer 101; w=2 -> Break -> defer 102
  ' Fn exit: defer 999
  Assert g_logLen = 9, "loop break/continue defer count"
  Assert g_log(0) = 1, "i=1 body"
  Assert g_log(1) = 10, "i=1 defer"
  Assert g_log(2) = 22, "i=2 inner defer on Continue"
  Assert g_log(3) = 20, "i=2 outer defer on Continue"
  Assert g_log(4) = 33, "i=3 inner defer on Break"
  Assert g_log(5) = 30, "i=3 outer defer on Break"
  Assert g_log(6) = 101, "w=1 defer on Continue"
  Assert g_log(7) = 102, "w=2 defer on Break"
  Assert g_log(8) = 999, "function outer defer"

  ResetLog()
  NestedDeferAndInnerLoop()
  Assert (g_logLen = 4) And (g_log(0) = 1) And (g_log(1) = 10) And (g_log(2) = 11) And (g_log(3) = 12), "nested defer + inner loop Break/Continue"

  ResetLog()
  WithAndUnionMatchDefer(OptSome(6))
  Assert (g_logLen = 4) And (g_log(0) = 11) And (g_log(1) = 20) And (g_log(2) = 6) And (g_log(3) = 60), "With and Union Match Defer"

  ResetLog()
  GotoUnwindsInnerDefer(True)
  Assert (g_logLen = 3) And (g_log(0) = 50) And (g_log(1) = 20) And (g_log(2) = 100), "GoTo to outer label unwinds inner defer"

  ResetLog()
  Assert GenericDeferAdd(15, 27) = 42, "generic function with Defer"
  Assert (g_logLen = 1) And (g_log(0) = 42), "generic Defer ran"

  Dim t As Task(Integer) = AsyncDeferWork(14)
  Assert (Await t) = 42, "Async function with Defer HeapFree"

  Assert ArenaAndHeapCleanup(10) = 25, "ArenaDestroy + HeapFree via Defer"

  ' Defer containing Assert inside a Test block must compile cleanly (no codegen recursion!)
  Dim testCleanup As Integer = 1
  Defer Assert testCleanup = 1, "Defer Assert inside Test"
End Test

Test "Type Analyzer rejects illegal control flow in Defer, --ast prints Defer, and arm-m cross-compiles"
  Dim comp(768), tmpDir(256), srcPath(300), objPath(300) As Byte
  GetCompilerAbs(comp, 768)
  Dim pid As Integer = getpid()
  snprintf(tmpDir, 256, "/tmp/vb_defer_%d", pid)
  snprintf(srcPath, 300, "%s/neg.vb", tmpDir)
  snprintf(objPath, 300, "%s/arm.o", tmpDir)
  Dim cmd(1536) As Byte
  snprintf(cmd, 1536, "mkdir -p '%s'", tmpDir)
  system(cmd)

  ' 1. Return inside Defer
  WriteTextFile(srcPath, "Function main() As Integer\n  Defer Return 1\n  Return 0\nEnd Function\n")
  snprintf(cmd, 1536, "'%s' '%s' --emit-only", comp, srcPath)
  Assert RunShell(cmd) <> 0, "Return inside Defer must fail"
  Assert OutHas("'Return' cannot be used inside a Defer block"), "error reports Return in Defer"

  ' 2. Break escaping Defer
  WriteTextFile(srcPath, "Function main() As Integer\n  While True Do\n    Defer Break\n    Break\n  End While\n  Return 0\nEnd Function\n")
  snprintf(cmd, 1536, "'%s' '%s' --emit-only", comp, srcPath)
  Assert RunShell(cmd) <> 0, "Break escaping Defer must fail"
  Assert OutHas("'Break' cannot escape a Defer block"), "error reports Break escaping Defer"

  ' 3. Continue escaping Defer
  WriteTextFile(srcPath, "Function main() As Integer\n  For i = 0 To 2 Do\n    Defer Continue\n  End For\n  Return 0\nEnd Function\n")
  snprintf(cmd, 1536, "'%s' '%s' --emit-only", comp, srcPath)
  Assert RunShell(cmd) <> 0, "Continue escaping Defer must fail"
  Assert OutHas("'Continue' cannot escape a Defer block"), "error reports Continue escaping Defer"

  ' 4. GoTo inside Defer
  WriteTextFile(srcPath, "Function main() As Integer\n  Label out\n  Defer GoTo out\n  Return 0\nEnd Function\n")
  snprintf(cmd, 1536, "'%s' '%s' --emit-only", comp, srcPath)
  Assert RunShell(cmd) <> 0, "GoTo inside Defer must fail"
  Assert OutHas("'GoTo' cannot be used inside a Defer block"), "error reports GoTo in Defer"

  ' 5. Label inside Defer
  WriteTextFile(srcPath, "Function main() As Integer\n  Defer\n    Label bad\n  End Defer\n  Return 0\nEnd Function\n")
  snprintf(cmd, 1536, "'%s' '%s' --emit-only", comp, srcPath)
  Assert RunShell(cmd) <> 0, "Label inside Defer must fail"
  Assert OutHas("'Label' cannot be declared inside a Defer block"), "error reports Label in Defer"

  ' 6. GoTo jumping across Defer
  WriteTextFile(srcPath, "Function main() As Integer\n  GoTo skip\n  Defer Printf(\"x\")\n  Label skip\n  Return 0\nEnd Function\n")
  snprintf(cmd, 1536, "'%s' '%s' --emit-only", comp, srcPath)
  Assert RunShell(cmd) <> 0, "GoTo jumping over Defer must fail"
  Assert OutHas("'GoTo' cannot jump across a Defer statement"), "error reports GoTo crossing Defer"

  ' 7. --ast prints both inline and block Defer, and arm-m compiles freestanding
  WriteTextFile(srcPath, "Function main() As Integer\n  Dim x As Integer = 1\n  Defer x = 2\n  Defer\n    x = 3\n  End Defer\n  Return 0\nEnd Function\n")
  snprintf(cmd, 1536, "'%s' --ast --emit-only '%s'", comp, srcPath)
  Assert RunShell(cmd) = 0, "--ast on Defer succeeds"
  Assert OutHas("Defer x = 2"), "--ast prints inline Defer"
  Assert OutHas("End Defer"), "--ast prints block Defer"

  snprintf(cmd, 1536, "'%s' --target arm-m -c '%s' -o '%s'", comp, srcPath, objPath)
  Assert RunShell(cmd) = 0, "arm-m cross-compile with Defer succeeds"

  snprintf(cmd, 1536, "rm -rf '%s'", tmpDir)
  system(cmd)
End Test
