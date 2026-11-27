' VerBose Test Suite: Milestone 2 — Tagged Unions / Sum Types & Option/Result

Include "harness.vb"
Include "../std/result.vb"
Include "../std/strings.vb"

Structure Vec2
  Dim x, y As Double
End Structure

Union Shape
  Case Point
  Case Circle(radius As Double)
  Case Rect(w, h As Double)
  Case Moved(pos As Vec2, scale As Integer)
End Union

Union ColorTag
  Case Red
  Case Green
  Case Blue
End Union

Union NestedTree
  Case Leaf(v As Integer)
  Case OptNode(item As Option(Of Integer))
End Union

Function ShapeArea(s As Shape) As Double
  Match s With
    Case Point Then Return 0.0
    Case Circle(r) Then Return 3.0 * r * r
    Case Rect(w, h) Then Return w * h
    Case Moved(p, sc) Then Return (p.x + p.y) * Double(sc)
  End Match
End Function

Function ShapeKindScore(s As Shape) As Integer
  Match s With
    Case Shape.Point Then Return 1
    Case Shape.Circle(_) Then Return 2
    Case Shape.Rect(w, _) When w > 10.0 Then Return 30
    Case Shape.Rect(_, _) Then Return 3
    Case Shape.Moved(_, sc) When sc < 0 Then Return -1
    Case _ Then Return 4
  End Match
End Function

Function SafeDiv(a, b As Integer) As Result(Of Integer, String)
  If b = 0 Then
    Return Result(Of Integer, String).Err("division by zero")
  End If
  Return Result(Of Integer, String).Ok(a / b)
End Function

Function FindPositive(arr() As Integer, n As Integer) As Option(Of Integer)
  For i = 0 To n - 1 Do
    If arr(i) > 0 Then
      Return OptSome(arr(i))
    End If
  End For
  Return OptNone(Of Integer)()
End Function

Async Function AsyncFetch(ok As Boolean) As Result(Of Integer, String)
  If ok Then
    Return ResOk(Of Integer, String)(777)
  End If
  Return ResErr(Of Integer, String)("network timeout")
End Function

Operator =(a As ColorTag, b As ColorTag) As Boolean
  Dim ta = 0, tb = 0 As Integer
  Match a With
    Case Red Then ta = 0
    Case Green Then ta = 1
    Case Blue Then ta = 2
  End Match
  Match b With
    Case Red Then tb = 0
    Case Green Then tb = 1
    Case Blue Then tb = 2
  End Match
  Return ta = tb
End Operator

Test "concrete tagged union construction, layout, SizeOf, and Match destructuring"
  Dim p As Shape = Shape.Point
  Dim c As Shape = Shape.Circle(4.0)
  Dim r As Shape = Shape.Rect(3.0, 5.0)
  Dim m As Shape = Moved(Vec2(2.0, 6.0), 10)
  Dim uqC As Shape = Circle(2.0)
  Dim uqP As Shape = Point

  Assert SizeOf(ColorTag) = 4, "unit-only union is 4 bytes (i32 tag only)"
  Assert SizeOf(Shape) = 32, "Shape: i32 tag + pad + max payload (Vec2+i32 = 24B) = 32B"

  Assert ShapeArea(p) = 0.0, "Point area"
  Assert ShapeArea(uqP) = 0.0, "unqualified Point area"
  Assert ShapeArea(c) = 48.0, "Circle area (3 * 4 * 4)"
  Assert ShapeArea(uqC) = 12.0, "unqualified Circle area"
  Assert ShapeArea(r) = 15.0, "Rect area (3 * 5)"
  Assert ShapeArea(m) = 80.0, "Moved area ((2+6)*10)"

  Assert ShapeKindScore(p) = 1, "qualified Point match"
  Assert ShapeKindScore(c) = 2, "qualified Circle(_) match"
  Assert ShapeKindScore(Shape.Rect(20.0, 1.0)) = 30, "guarded Rect match (w > 10)"
  Assert ShapeKindScore(r) = 3, "unguarded fallback Rect match"
  Assert ShapeKindScore(Moved(Vec2(1.0, 1.0), -3)) = -1, "guarded Moved match (sc < 0)"
  Assert ShapeKindScore(m) = 4, "wildcard fallback match"

  ' Multi-variant pattern (without bindings)
  Dim isSmall As Boolean = False
  Match c With
    Case Point, Circle(_) Then isSmall = True
    Case Rect(_, _), Moved(_, _) Then isSmall = False
  End Match
  Assert isSmall, "multi-variant Case pattern matches Circle(_)"

  ' Operator overloading on Union
  Assert ColorTag.Red = Red, "Union Operator = (equal)"
  Assert Not (ColorTag.Red = Blue), "Union Operator = (not equal)"

  ' Uninitialized union defaults to tag 0 (first variant)
  Dim defShape As Shape
  Assert ShapeKindScore(defShape) = 1, "zero-initialized union defaults to first variant (tag 0)"
End Test

Test "generic Option(Of T) and Result(Of T, E) from std/result.vb"
  Dim nums(4) As Integer = (-5, -1, 42, 99)
  Dim negs(3) As Integer = (-9, -4, -1)

  Dim o1 As Option(Of Integer) = FindPositive(nums, 4)
  Dim o2 As Option(Of Integer) = FindPositive(negs, 3)

  Assert OptIsSome(o1), "OptIsSome inferred T=Integer"
  Assert Not OptIsNone(o1), "Not OptIsNone"
  Assert OptUnwrap(o1) = 42, "OptUnwrap returns 42"
  Assert OptUnwrapOr(o1, -1) = 42, "OptUnwrapOr returns Some value"

  Assert OptIsNone(o2), "OptIsNone on None"
  Assert OptUnwrapOr(o2, -1) = -1, "OptUnwrapOr returns default on None"

  ' Zero-initialized Option(Of T) defaults to None (tag 0)
  Dim defOpt As Option(Of Double)
  Assert OptIsNone(defOpt), "default-initialized Option(Of Double) is None"

  ' Result(Of Integer, String)
  Dim rOk As Result(Of Integer, String) = SafeDiv(100, 4)
  Dim rErr As Result(Of Integer, String) = SafeDiv(100, 0)

  Assert ResIsOk(rOk), "ResIsOk inferred T, E"
  Assert ResUnwrap(rOk) = 25, "ResUnwrap returns 25"
  Assert ResUnwrapOr(rOk, -1) = 25, "ResUnwrapOr on Ok"
  Assert ResIsErr(rErr), "ResIsErr on Err"
  Assert ResUnwrapOr(rErr, -1) = -1, "ResUnwrapOr on Err returns default"
  Assert StrEq(ResUnwrapErr(rErr), "division by zero"), "ResUnwrapErr returns message"

  ' Direct Match on rvalue Result expression with guards
  Dim outCode As Integer = 0
  Match SafeDiv(50, 2) With
    Case Ok(v) When v > 20 Then outCode = v
    Case Ok(v) Then outCode = -v
    Case Err(_) Then outCode = -999
  End Match
  Assert outCode = 25, "Match on rvalue Result with When guard"

  ' Convert Result -> Option
  Assert OptUnwrapOr(ResOkOpt(rOk), 0) = 25, "ResOkOpt(Ok) -> Some(25)"
  Assert OptIsNone(ResOkOpt(rErr)), "ResOkOpt(Err) -> None"
  Assert OptIsSome(ResErrOpt(rErr)), "ResErrOpt(Err) -> Some(err)"
  Assert OptIsNone(ResErrOpt(rOk)), "ResErrOpt(Ok) -> None"
End Test

Test "nested generic unions, arrays of unions, ArenaAlloc, and Async/Await"
  ' Nested Option(Of Result(Of Integer, Integer))
  Dim nested As Option(Of Result(Of Integer, Integer)) = Option(Of Result(Of Integer, Integer)).Some(Result(Of Integer, Integer).Ok(123))
  Dim extracted As Integer = 0
  Match nested With
    Case Some(inner) Then
      Match inner With
        Case Ok(v) Then extracted = v
        Case Err(e) Then extracted = -e
      End Match
    Case None Then extracted = -1
  End Match
  Assert extracted = 123, "nested Option(Of Result(Of Integer, Integer)) destructuring"

  ' Union containing generic Option
  Dim nt As NestedTree = NestedTree.OptNode(OptSome(88))
  Dim ntVal As Integer = 0
  Match nt With
    Case Leaf(v) Then ntVal = v
    Case OptNode(o) Then ntVal = OptUnwrapOr(o, -1)
  End Match
  Assert ntVal = 88, "union containing Option(Of Integer)"

  ' Fixed array & ArenaAlloc of unions
  Dim arena As Arena = ArenaCreate(4096)
  Dim arr() As Option(Of Integer) = ArenaAlloc(ByRef arena, Option(Of Integer), 4)
  arr(0) = OptNone(Of Integer)()
  arr(1) = OptSome(10)
  arr(2) = OptSome(20)
  arr(3) = OptNone(Of Integer)()
  Dim total As Integer = 0
  For i = 0 To 3 Do
    total = total + OptUnwrapOr(arr(i), 0)
  End For
  Assert total = 30, "ArenaAlloc slice of Option(Of Integer)"
  ArenaDestroy(ByRef arena)

  ' Async Function returning generic Result(Of Integer, String)
  Dim t1 As Task(Result(Of Integer, String)) = AsyncFetch(True)
  Dim t2 As Task(Result(Of Integer, String)) = AsyncFetch(False)
  Dim a1 As Result(Of Integer, String) = Await t1
  Dim a2 As Result(Of Integer, String) = Await t2
  Assert ResUnwrapOr(a1, 0) = 777, "Await Task(Result) Ok"
  Assert ResIsErr(a2), "Await Task(Result) Err"
End Test

Test "compile-time exhaustiveness, arity, type, and arm-m cross-compilation checks"
  Dim comp(768), tmpDir(256), srcPath(300) As Byte
  GetCompilerAbs(comp, 768)
  Dim pid As Integer = getpid()
  snprintf(tmpDir, 256, "/tmp/vb_union_%d", pid)
  snprintf(srcPath, 300, "%s/neg.vb", tmpDir)
  Dim cmd(1536) As Byte
  snprintf(cmd, 1536, "mkdir -p '%s'", tmpDir)
  system(cmd)

  ' 1. Non-exhaustive Match must fail at compile time listing missing variant
  WriteTextFile(srcPath, "Union Msg\n  Case Ping\n  Case Pong(n As Integer)\n  Case Quit\nEnd Union\nFunction main() As Integer\n  Dim m As Msg = Msg.Ping\n  Match m With\n    Case Ping Then Return 0\n    Case Pong(x) Then Return x\n  End Match\n  Return 0\nEnd Function\n")
  snprintf(cmd, 1536, "'%s' '%s' --emit-only", comp, srcPath)
  Assert RunShell(cmd) <> 0, "non-exhaustive Match must fail"
  Assert OutHas("Non-exhaustive Match on Union 'Msg'; missing variant(s): Quit"), "error names missing variant Quit"

  ' 2. Guarded-only arm does NOT satisfy exhaustiveness
  WriteTextFile(srcPath, "Union Flag\n  Case On(level As Integer)\n  Case Off\nEnd Union\nFunction main() As Integer\n  Dim f As Flag = Flag.Off\n  Match f With\n    Case On(lvl) When lvl > 0 Then Return lvl\n    Case Off Then Return 0\n  End Match\n  Return 0\nEnd Function\n")
  snprintf(cmd, 1536, "'%s' '%s' --emit-only", comp, srcPath)
  Assert RunShell(cmd) <> 0, "guarded-only arm is not exhaustive"
  Assert OutHas("missing variant(s): On"), "error names guarded-only variant On"

  ' 3. Wrong payload binding arity must fail
  WriteTextFile(srcPath, "Union PairU\n  Case Both(a, b As Integer)\n  Case Empty\nEnd Union\nFunction main() As Integer\n  Dim p As PairU = PairU.Empty\n  Match p With\n    Case Both(x) Then Return x\n    Case Empty Then Return 0\n  End Match\n  Return 0\nEnd Function\n")
  snprintf(cmd, 1536, "'%s' '%s' --emit-only", comp, srcPath)
  Assert RunShell(cmd) <> 0, "wrong binding count must fail"
  Assert OutHas("expects 2 payload binding(s), got 1"), "error reports binding arity mismatch"

  ' 4. Wrong constructor argument type must fail
  WriteTextFile(srcPath, "Union BoxU\n  Case Num(n As Integer)\nEnd Union\nFunction main() As Integer\n  Dim b As BoxU = BoxU.Num(\"hello\")\n  Return 0\nEnd Function\n")
  snprintf(cmd, 1536, "'%s' '%s' --emit-only", comp, srcPath)
  Assert RunShell(cmd) <> 0, "wrong payload arg type must fail"
  Assert OutHas("Type mismatch for argument 1 of union variant 'Num'"), "error reports payload arg type mismatch"

  ' 5. Freestanding arm-m cross-compilation of generic Option/Result (zero libc)
  Dim armSrc(300), armObj(300), armLl(300) As Byte
  snprintf(armSrc, 300, "%s/arm_union.vb", tmpDir)
  snprintf(armObj, 300, "%s/arm_union.o", tmpDir)
  snprintf(armLl, 300, "%s/arm_union.ll", tmpDir)
  WriteTextFile(armSrc, "Include \"std/result.vb\"\nPublic Function Compute(x As Integer) As Integer\n  Dim r As Result(Of Integer, Integer)\n  If x > 0 Then\n    r = ResOk(Of Integer, Integer)(x * 2)\n  Else\n    r = ResErr(Of Integer, Integer)(-1)\n  End If\n  Return ResUnwrapOr(r, 0)\nEnd Function\n")
  snprintf(cmd, 1536, "'%s' '%s' --target arm-m --emit-only -o '%s'", comp, armSrc, armLl)
  Assert RunShell(cmd) = 0, "arm-m IR emit for generic Result succeeds"
  Assert Not FileHas(armLl, "@printf"), "freestanding arm-m union emits zero libc printf"
  snprintf(cmd, 1536, "'%s' '%s' --target arm-m -c -o '%s'", comp, armSrc, armObj)
  Assert RunShell(cmd) = 0, "arm-m -c object compile succeeds"
  Assert FileExists(armObj), "arm-m .o produced"

  snprintf(cmd, 1536, "rm -rf '%s'", tmpDir)
  system(cmd)
End Test
