' Language Extensions test suite:
' - AddressOf (locals, globals, struct fields, array elements, function pointers & C qsort callback)
' - PtrRead, PtrWrite, PtrAdd (scalars, structs, byte-offset pointer stepping)
' - Label & GoTo (backward loops, forward cleanup jumps, dead code after GoTo/Return)
' - Structured Concurrency: Async Function/Sub, Task(T), Task, Await, Task arrays, struct returns, ARM-M freestanding

Include "harness.vb"

Declare Sub qsort(base As Pointer, nmemb, size As Long, compar As Pointer)

Public Structure Pair
  Dim a, b As Integer
End Structure

Private Static g_counter As Integer = 10

Public Function CompareInts(pa, pb As Pointer) As Integer
  Dim va = PtrRead(pa, Integer), vb = PtrRead(pb, Integer) As Integer
  If va < vb Then
    Return -1
  End If
  If va > vb Then
    Return 1
  End If
  Return 0
End Function

Private Static g_cmpFn As Pointer = AddressOf(CompareInts)

Public Async Function ComputeSquare(n As Integer) As Integer
  Dim acc As Integer = 0
  For i = 1 To n Do
    acc = acc + n
  End For
  Return acc
End Function

Public Async Function MakePairAsync(x, y As Integer) As Pair
  Dim p As Pair With ( a = x * 2, b = y * 3 )
  Return p
End Function

Public Async Function SumPairByVal(p As Pair) As Integer
  Return p.a + p.b
End Function

Public Async Sub BumpByRefAsync(ByRef target As Integer, delta As Integer)
  target = target + delta
End Sub

Private Function HelperAwait(t As Task(Integer)) As Integer
  Return Await t
End Function

Private Function LabelLoopAndCleanup(limit As Integer, failAt As Integer) As Integer
  Dim i = 0, sum = 0 As Integer
  Label retry
  i = i + 1
  If i = failAt Then
    GoTo cleanup
    sum = 999999
  End If
  sum = sum + i
  If i < limit Then
    GoTo retry
  End If
  Return sum
  Label cleanup
  Return -sum
End Function

Test "AddressOf + PtrRead/PtrWrite/PtrAdd on locals, globals, structs, and arrays"
  Dim x As Integer = 42
  Dim px As Pointer = AddressOf(x)
  Assert PtrRead(px, Integer) = 42
  PtrWrite(px, Integer, 99)
  Assert x = 99

  Dim pg As Pointer = AddressOf(g_counter)
  Assert PtrRead(pg, Integer) = 10
  PtrWrite(pg, Integer, 77)
  Assert g_counter = 77
  g_counter = 10

  Dim p As Pair With ( a = 11, b = 22 )
  Dim pb As Pointer = AddressOf(p.b)
  PtrWrite(pb, Integer, 55)
  Assert (p.a = 11) And (p.b = 55)

  ' Struct read/write through raw Pointer
  Dim p2 As Pair
  PtrWrite(AddressOf(p2), Pair, p)
  Dim p3 As Pair = PtrRead(AddressOf(p2), Pair)
  Assert (p3.a = 11) And (p3.b = 55)

  ' Array element AddressOf + PtrAdd byte stepping
  Dim nums(4) As Integer = (10, 20, 30, 40)
  Dim p0 As Pointer = AddressOf(nums(0))
  Dim p2Elem As Pointer = PtrAdd(p0, 2 * SizeOf(Integer))
  Assert PtrRead(p2Elem, Integer) = 30
  PtrWrite(p2Elem, Integer, 300)
  Assert nums(2) = 300
End Test

Test "AddressOf(Function) and Static const-init callback passed to C qsort"
  Dim arr(5) As Integer = (42, 7, 99, 1, 18)
  Assert Not IsNull(g_cmpFn)
  Assert Not IsNull(AddressOf(CompareInts))
  qsort(AddressOf(arr(0)), 5, SizeOf(Integer), g_cmpFn)
  Assert (arr(0) = 1) And (arr(1) = 7) And (arr(2) = 18) And (arr(3) = 42) And (arr(4) = 99)
End Test

Test "Label and GoTo: backward loops, forward cleanup jumps, and dead code skipping"
  Assert LabelLoopAndCleanup(5, 99) = 15
  Assert LabelLoopAndCleanup(5, 4) = -6

  ' Local Label/GoTo directly inside a Test block
  Dim acc As Integer = 0
  Dim k As Integer = 0
  Label nextStep
  k = k + 1
  acc = acc + k
  If k < 4 Then
    GoTo nextStep
  End If
  Assert acc = 10
End Test

Test "Async/Await/Task(T): parallel tasks, Task arrays, structs, Async Sub, ByRef, helper param"
  Dim t1 As Task(Integer) = ComputeSquare(12)
  Dim t2 As Task(Integer) = ComputeSquare(25)
  Assert (Not IsNull(t1)) And (Not IsNull(t2))
  Assert ((Await t1) + (Await t2)) = 769

  ' Direct Await + helper taking Task(Integer)
  Assert (Await ComputeSquare(7)) = 49
  Assert HelperAwait(ComputeSquare(9)) = 81

  ' Array of Task(Integer) launched concurrently
  Dim tasks(4) As Task(Integer)
  For i = 0 To 3 Do
    tasks(i) = ComputeSquare(i + 2)
  End For
  Dim total As Integer = 0
  For i = 0 To 3 Do
    total = total + (Await tasks(i))
  End For
  Assert total = 54

  ' Struct return and ByVal struct parameter copy isolation
  Dim pt As Task(Pair) = MakePairAsync(5, 7)
  Dim gotP As Pair = Await pt
  Assert (gotP.a = 10) And (gotP.b = 21)

  Dim src As Pair With ( a = 13, b = 29 )
  Dim st As Task(Integer) = SumPairByVal(src)
  src.a = 999
  Assert (Await st) = 42

  ' Async Sub returning Task + ByRef mutation
  Dim sharedVal As Integer = 100
  Dim w As Task = BumpByRefAsync(ByRef sharedVal, 23)
  Await w
  Assert sharedVal = 123
End Test

Test "Async/Await on freestanding --target arm-m compiles to Thumb .o with zero pthread symbols"
  Dim comp(768), tmpVb(256), tmpLl(256), tmpObj(256) As Byte
  GetCompilerAbs(comp, 768)
  Dim pid As Integer = getpid()
  snprintf(tmpVb, 256, "/tmp/vb_arm_async_%d.vb", pid)
  snprintf(tmpLl, 256, "/tmp/vb_arm_async_%d.ll", pid)
  snprintf(tmpObj, 256, "/tmp/vb_arm_async_%d.o", pid)

  WriteTextFile(tmpVb, "Public Async Function AddOne(x As Integer) As Integer\n  Return x + 1\nEnd Function\nPublic Function run_arm() As Integer\n  Dim t As Task(Integer) = AddOne(41)\n  Return Await t\nEnd Function\n")

  Dim cmd(1536) As Byte
  snprintf(cmd, 1536, "'%s' '%s' --target arm-m --emit-only -o '%s'", comp, tmpVb, tmpLl)
  Assert RunShell(cmd) = 0
  Assert FileHas(tmpLl, "thumbv7em-unknown-none-eabi")
  Assert Not FileHas(tmpLl, "pthread_create")
  Assert Not FileHas(tmpLl, "pthread_join")

  snprintf(cmd, 1536, "'%s' '%s' --target arm-m -c -o '%s'", comp, tmpVb, tmpObj)
  Assert RunShell(cmd) = 0
  Assert FileExists(tmpObj)

  unlink(tmpVb)
  unlink(tmpLl)
  unlink(tmpObj)
End Test

Test "Type Analyzer rejects invalid AddressOf, Ptr*, Label/GoTo, and Async/Await usage"
  Dim comp(768), f1(256), f2(256), f3(256), f4(256), f5(256), f6(256) As Byte
  GetCompilerAbs(comp, 768)
  Dim pid As Integer = getpid()
  snprintf(f1, 256, "/tmp/vb_le_err1_%d.vb", pid)
  snprintf(f2, 256, "/tmp/vb_le_err2_%d.vb", pid)
  snprintf(f3, 256, "/tmp/vb_le_err3_%d.vb", pid)
  snprintf(f4, 256, "/tmp/vb_le_err4_%d.vb", pid)
  snprintf(f5, 256, "/tmp/vb_le_err5_%d.vb", pid)
  snprintf(f6, 256, "/tmp/vb_le_err6_%d.vb", pid)

  WriteTextFile(f1, "Public Function main() As Integer\n  Label dup\n  Label dup\n  Return 0\nEnd Function\n")
  WriteTextFile(f2, "Public Function main() As Integer\n  GoTo nowhere\n  Return 0\nEnd Function\n")
  WriteTextFile(f3, "Public Function main() As Integer\n  Dim p As Pointer = AddressOf(123)\n  Return 0\nEnd Function\n")
  WriteTextFile(f4, "Public Function main() As Integer\n  Dim x As Integer = Await 42\n  Return x\nEnd Function\n")
  WriteTextFile(f5, "Public Async Function main() As Integer\n  Return 0\nEnd Function\n")
  WriteTextFile(f6, "Public Function main() As Integer\n  Dim p As Pointer = 0\n  Dim q As Pointer = PtrAdd(p, 1.5)\n  Return 0\nEnd Function\n")

  Dim cmd(1024) As Byte
  snprintf(cmd, 1024, "'%s' '%s' --emit-only", comp, f1)
  Assert RunShell(cmd) <> 0
  Assert OutHas("Duplicate label 'dup'")

  snprintf(cmd, 1024, "'%s' '%s' --emit-only", comp, f2)
  Assert RunShell(cmd) <> 0
  Assert OutHas("Undefined GoTo label 'nowhere'")

  snprintf(cmd, 1024, "'%s' '%s' --emit-only", comp, f3)
  Assert RunShell(cmd) <> 0
  Assert OutHas("AddressOf requires a variable")

  snprintf(cmd, 1024, "'%s' '%s' --emit-only", comp, f4)
  Assert RunShell(cmd) <> 0
  Assert OutHas("Await requires a Task expression")

  snprintf(cmd, 1024, "'%s' '%s' --emit-only", comp, f5)
  Assert RunShell(cmd) <> 0
  Assert OutHas("Entry point 'main' cannot be Async")

  snprintf(cmd, 1024, "'%s' '%s' --emit-only", comp, f6)
  Assert RunShell(cmd) <> 0
  Assert OutHas("PtrAdd offset must be an integer type")

  unlink(f1)
  unlink(f2)
  unlink(f3)
  unlink(f4)
  unlink(f5)
  unlink(f6)
End Test
