' Tests for short-circuiting boolean And/Or, rvalue member/index access,
' and multi-line function/constructor call arguments.

Declare Function strcmp(a As Pointer, b As Pointer) As Integer

Structure ExprVec2
  Dim x, y As Single
End Structure

Structure ExprTransform
  Dim pos As ExprVec2
  Dim scale As Single
End Structure

Structure ExprShip
  Dim id As Integer
  Dim tf As ExprTransform
  Dim tags(4) As Integer
End Structure

Private Static g_scHits As Integer = 0

Private Function ScBump(ret As Boolean) As Boolean
  g_scHits = g_scHits + 1
  Return ret
End Function

Private Function MakeVec2(x, y As Single) As ExprVec2
  Dim v As ExprVec2 With ( x = x, y = y )
  Return v
End Function

Private Function MakeShip(id As Integer, px, py As Single) As ExprShip
  Dim s As ExprShip With (
    id = id,
    tf = ExprTransform With (
      pos = ExprVec2 With ( x = px, y = py ),
      scale = 2.5
    )
  )
  s.tags(0) = 10
  s.tags(1) = 20
  s.tags(2) = 30
  s.tags(3) = 40
  Return s
End Function

Private Async Function MakeShipAsync(id As Integer) As ExprShip
  Return MakeShip(id, 13.0, 29.0)
End Function

Private Function MakeLabel() As String
  Return "VerBose"
End Function

Private Function MakeBytes(ByRef a As Arena) As Pointer
  Dim p As Pointer = ArenaAllocBytes(ByRef a, 4)
  PtrWrite(p, Byte, 65)
  PtrWrite(PtrAdd(p, 1), Byte, 66)
  Return p
End Function

Private Function Sum4(a, b, c, d As Integer) As Integer
  Return a + b + c + d
End Function

Private Sub Accumulate(ByRef total As Integer, delta As Integer, mult As Integer)
  total = total + delta * mult
End Sub

Test "short-circuit boolean And/Or skips RHS side effects and traps"
  g_scHits = 0

  ' False And RHS must not evaluate RHS
  Dim r1 As Boolean = False And ScBump(True)
  Assert r1 = False
  Assert g_scHits = 0

  ' True And RHS must evaluate RHS
  Dim r2 As Boolean = True And ScBump(True)
  Assert r2 = True
  Assert g_scHits = 1

  ' True Or RHS must not evaluate RHS
  Dim r3 As Boolean = True Or ScBump(False)
  Assert r3 = True
  Assert g_scHits = 1

  ' False Or RHS must evaluate RHS
  Dim r4 As Boolean = False Or ScBump(True)
  Assert r4 = True
  Assert g_scHits = 2

  ' Guarded null-pointer strcmp (would SIGSEGV without short-circuiting)
  Dim nullPtr As Pointer = 0
  Dim hitNull As Boolean = False
  If (Not IsNull(nullPtr)) And (strcmp(nullPtr, "hello") = 0) Then
    hitNull = True
  End If
  Assert hitNull = False

  ' Guarded division by zero (would SIGFPE without short-circuiting)
  Dim zero As Integer = 0
  Dim divAnd As Boolean = (zero <> 0) And ((100 / zero) > 1)
  Dim divOr As Boolean = (zero = 0) Or ((100 / zero) = 1)
  Assert divAnd = False
  Assert divOr = True

  ' Nested short-circuit combinations (verifies SSA phi dominance across nested blocks)
  g_scHits = 0
  Dim nested As Boolean = (False And ScBump(True)) Or (True And (True Or ScBump(False)))
  Assert nested = True
  Assert g_scHits = 0

  ' Guarded loop condition with bounds check
  Dim arr(4) As Integer = (1, 2, 32, 32)
  Dim hi As Integer = 4
  While (hi > 0) And (arr(hi - 1) = 32) Do
    hi = hi - 1
  End While
  Assert hi = 2

  ' Bitwise integer And / Or still work as bitwise operators
  Dim mask As Integer = (1 Shl 4) Or (8 Shr 1)
  Assert mask = 20
  Assert (mask And 4) = 4
  Assert (mask And 2) = 0
End Test

Test "rvalue member access and chained rvalue indexing"
  Assert MakeVec2(3.5, 7.25).x = 3.5
  Assert MakeVec2(3.5, 7.25).y = 7.25

  ' Deeply nested struct fields directly off function return value
  Assert MakeShip(42, 9.0, 8.0).id = 42
  Assert MakeShip(42, 9.0, 8.0).tf.scale = 2.5
  Assert MakeShip(42, 9.0, 8.0).tf.pos.x = 9.0
  Assert MakeShip(42, 9.0, 8.0).tf.pos.y = 8.0

  ' Fixed-array field indexed directly off rvalue struct
  Assert MakeShip(1, 0.0, 0.0).tags(2) = 30

  ' Inline With (...) struct initializer rvalue field access
  Assert (ExprVec2 With ( x = 11.0, y = 22.0 )).y = 22.0

  ' Rvalue string/pointer indexing & ArenaAlloc rvalue indexing
  Assert MakeLabel()(0) = 86
  Assert MakeLabel()(3) = 66
  Dim a As Arena = ArenaCreate(256)
  Assert MakeBytes(ByRef a)(1) = 66
  Assert ArenaAlloc(ByRef a, Integer, 3)(1) = 0
  ArenaDestroy(ByRef a)

  ' Async Task(ExprShip) awaited rvalue member chain
  Dim job As Task(ExprShip) = MakeShipAsync(77)
  Assert (Await job).tf.pos.y = 29.0
End Test

Test "multi-line function and struct constructor call arguments"
  Dim total As Integer = Sum4(
    10,
    20,
    30,
    40
  )
  Assert total = 100

  ' Multi-line call with ByRef argument
  Accumulate(
    ByRef total,
    5,
    3
  )
  Assert total = 115

  ' Nested multi-line calls combined with rvalue field access
  Dim px As Single = MakeShip(
    7,
    MakeVec2(
      4.0,
      5.0
    ).x,
    MakeVec2(
      6.0,
      9.0
    ).y
  ).tf.pos.y
  Assert px = 9.0

  ' Multi-line struct constructor initializer
  Dim v As ExprVec2(
    12.5,
    24.5
  )
  Assert v.x = 12.5
  Assert v.y = 24.5
End Test
