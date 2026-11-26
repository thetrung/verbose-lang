' VerBose-Lang Test Suite: Generics (Monomorphized Parametric Structures, Functions, Subs, Operators)

Include "harness.vb"
Include "std/span.vb"
Include "std/collections.vb"

Public Structure Box(Of T)
  Dim value As T
End Structure

Public Structure Pair(Of A, B)
  Dim first As A
  Dim second As B
End Structure

Public Structure Vec2(Of T)
  Dim x, y As T
End Structure

Private Structure LocalVertex
  Dim id As Integer
  Dim weight As Single
End Structure

Public Function Identity(Of T)(x As T) As T
  Return x
End Function

Public Sub Swap(Of T)(ByRef a, b As T)
  Dim tmp As T = a
  a = b
  b = tmp
End Sub

Public Function MakePair(Of A, B)(a As A, b As B) As Pair(Of A, B)
  Return Pair(Of A, B) With ( first = a, second = b )
End Function

Public Function UnboxPairFirst(Of A, B)(b As Box(Of Pair(Of A, B))) As A
  Return b.value.first
End Function

Public Function MaxVal(Of T)(a, b As T) As T
  If a > b Then
    Return a
  End If
  Return b
End Function

Public Function CastSum(Of T)(a, b As Integer) As T
  Return T(a) + T(b)
End Function

Public Function GenFact(Of T)(n As T) As T
  If n <= T(1) Then
    Return T(1)
  End If
  Return n * GenFact(Of T)(n - T(1))
End Function

Public Function SumSlice(Of T)(arr() As T, count As Integer) As T
  Dim acc As T = T(0)
  For i = 0 To count - 1 Do
    acc = acc + arr(i)
  End For
  Return acc
End Function

Public Operator +(Of T)(a, b As Vec2(Of T)) As Vec2(Of T)
  Return Vec2(Of T) With ( x = a.x + b.x, y = a.y + b.y )
End Operator

Public Operator *(Of T)(v As Vec2(Of T), s As T) As Vec2(Of T)
  Return Vec2(Of T) With ( x = v.x * s, y = v.y * s )
End Operator

Public Operator -(Of T)(v As Vec2(Of T)) As Vec2(Of T)
  Return Vec2(Of T) With ( x = -v.x, y = -v.y )
End Operator

Public Operator =(Of T)(a, b As Vec2(Of T)) As Boolean
  Return (a.x = b.x) And (a.y = b.y)
End Operator

Public Async Function AsyncWrap(Of T)(val As T) As Box(Of T)
  Return Box(Of T) With ( value = val )
End Function

Private Static g_staticBox As Box(Of Integer) With ( value = 77 )

Test "Generic Structures: primitives, structs, nested generics, arrays, and Static"
  Dim bi As Box(Of Integer) With ( value = 42 )
  Dim bs As Box(Of Single) = Box(Of Single)(3.5)
  Dim bd As Box(Of Double)(2.25)
  Assert bi.value = 42, "Box(Of Integer) With init"
  Assert bs.value = 3.5, "Box(Of Single) positional init"
  Assert bd.value = 2.25, "Box(Of Double) inline positional init"
  Assert g_staticBox.value = 77, "Static generic struct global"

  Dim p1 As Pair(Of Integer, Single) = Pair(Of Integer, Single)(10, 2.5)
  Dim p2 As Pair(Of Single, Integer) = Pair(Of Single, Integer) With ( first = 7.5, second = 99 )
  Assert (p1.first = 10) And (p1.second = 2.5), "Pair(Of Integer, Single)"
  Assert (p2.first = 7.5) And (p2.second = 99), "Pair(Of Single, Integer)"

  ' Collision-free nested generic structures (Pair(Pair(I,B),S) vs Pair(I,Pair(B,S)))
  Dim n1 As Pair(Of Pair(Of Integer, Byte), Single) = Pair(Of Pair(Of Integer, Byte), Single)(Pair(Of Integer, Byte)(100, Byte(7)), 1.5)
  Dim n2 As Pair(Of Integer, Pair(Of Byte, Single)) = Pair(Of Integer, Pair(Of Byte, Single))(200, Pair(Of Byte, Single)(Byte(9), 2.5))
  Assert (n1.first.first = 100) And (Integer(n1.first.second) = 7) And (n1.second = 1.5), "Left-nested Pair"
  Assert (n2.first = 200) And (Integer(n2.second.first) = 9) And (n2.second.second = 2.5), "Right-nested Pair"

  ' Fixed array of generic structs
  Dim boxes(3) As Box(Of Integer)
  For i = 0 To 2 Do
    boxes(i) = Box(Of Integer)((i + 1) * 11)
  End For
  Assert (boxes(0).value = 11) And (boxes(1).value = 22) And (boxes(2).value = 33), "Fixed array of Box(Of Integer)"

  ' SizeOf on generic structs
  Assert SizeOf(Box(Of Byte)) = 1, "SizeOf(Box(Of Byte)) = 1"
  Assert SizeOf(Box(Of Long)) = 8, "SizeOf(Box(Of Long)) = 8"

  ' Inferred generic struct construction, With block, and rvalue field access
  Dim infB1 As Box(Of Integer) = Box(88)
  Dim infB2 As Box(Of Integer) = Box With ( value = 99 )
  With infB1
    .value = .value + 12
  End With
  Assert (infB1.value = 100) And (infB2.value = 99), "Inferred generic struct ctor/With and With block"
  Assert Identity(MakePair(10, 2.5).second) = 2.5, "Rvalue field access fed into inferred generic call"
End Test

Test "Generic Functions & Subs: explicit (Of ...) and call-site type inference"
  ' Explicit type arguments
  Assert Identity(Of Integer)(123) = 123, "Identity(Of Integer)"
  Assert Identity(Of Double)(6.25) = 6.25, "Identity(Of Double)"
  Dim pExp As Pair(Of Integer, Boolean) = MakePair(Of Integer, Boolean)(7, True)
  Assert (pExp.first = 7) And pExp.second, "MakePair explicit"

  ' Inferred type arguments
  Assert Identity(456) = 456, "Identity inferred Integer"
  Dim sVal As Single = 1.5
  Assert Identity(sVal) = 1.5, "Identity inferred Single"
  Assert MaxVal(10, 25) = 25, "MaxVal inferred Integer"
  Dim f1 = Single(3.25), f2 = Single(8.5) As Single
  Assert MaxVal(f1, f2) = 8.5, "MaxVal inferred Single"
  Assert MaxVal(f1, 1.0) = 3.25, "MaxVal contextual float literal"

  ' Generic Sub with ByRef
  Dim a = 10, b = 99 As Integer
  Swap(ByRef a, ByRef b)
  Assert (a = 99) And (b = 10), "Swap inferred Integer"

  Dim pa As Pair(Of Integer, Single) = Pair(Of Integer, Single)(1, 2.0)
  Dim pb As Pair(Of Integer, Single) = Pair(Of Integer, Single)(3, 4.0)
  Swap(ByRef pa, ByRef pb)
  Assert (pa.first = 3) And (pa.second = 4.0) And (pb.first = 1) And (pb.second = 2.0), "Swap inferred generic struct"

  ' Deep nested generic inference: Box(Of Pair(Of A, B)) -> A
  Dim nested As Box(Of Pair(Of Long, Byte)) = Box(Of Pair(Of Long, Byte))(Pair(Of Long, Byte)(Long(9000000000), Byte(5)))
  Dim firstVal As Long = UnboxPairFirst(nested)
  Assert firstVal = Long(9000000000), "Deep nested generic pattern inference"

  ' T(x) conversion, recursion, and generic slice parameter
  Dim cf As Single = CastSum(Of Single)(3, 4)
  Assert cf = 7.0, "CastSum(Of Single)"
  Assert GenFact(Of Integer)(5) = 120, "Recursive GenFact(Of Integer)"
  Assert GenFact(Of Long)(6) = Long(720), "Recursive GenFact(Of Long)"

  Dim nums(4) As Integer = (10, 20, 30, 40)
  Assert SumSlice(nums, 4) = 100, "SumSlice inferred from fixed Integer array"
  Dim floats(3) As Single = (1.5, 2.5, 3.0)
  Assert SumSlice(floats, 3) = 7.0, "SumSlice inferred from fixed Single array"
End Test

Test "Generic Operator Overloading over Vec2(Of T)"
  Dim vi1 As Vec2(Of Integer) = Vec2(Of Integer)(1, 2)
  Dim vi2 As Vec2(Of Integer) = Vec2(Of Integer)(3, 4)
  Dim viSum As Vec2(Of Integer) = (vi1 + vi2) * 3
  Dim viExp As Vec2(Of Integer) = Vec2(Of Integer)(12, 18)
  Assert viSum = viExp, "Vec2(Of Integer) +, *, ="
  Dim viNeg As Vec2(Of Integer) = -vi1
  Assert (viNeg.x = -1) And (viNeg.y = -2), "Vec2(Of Integer) unary -"

  Dim vf1 As Vec2(Of Single) = Vec2(Of Single)(1.5, 2.0)
  Dim vf2 As Vec2(Of Single) = Vec2(Of Single)(0.5, 3.0)
  Dim vfRes As Vec2(Of Single) = -(vf1 + vf2) * 2.0
  Assert (vfRes.x = -4.0) And (vfRes.y = -10.0), "Vec2(Of Single) chained -, +, * with float lit"
End Test

Test "Generic Async/Await and AddressOf generic function"
  Dim t1 As Task(Box(Of Integer)) = AsyncWrap(Of Integer)(55)
  Dim t2 As Task(Box(Of Double)) = AsyncWrap(8.75)
  Dim r1 As Box(Of Integer) = Await t1
  Dim r2 As Box(Of Double) = Await t2
  Assert r1.value = 55, "AsyncWrap(Of Integer)"
  Assert r2.value = 8.75, "AsyncWrap inferred Double"

  Dim fnPtr As Pointer = AddressOf(Identity(Of Integer))
  Assert Not IsNull(fnPtr), "AddressOf(Identity(Of Integer)) must be non-null"
End Test

Test "Generic Stdlib: Span(Of T), Vec(Of T), Ring(Of T) with primitives and Private struct"
  Dim arena As Arena = ArenaCreate(16384)

  ' Span(Of LocalVertex) - Private struct passed into cross-module Public generic!
  Dim rawV() As LocalVertex = ArenaAlloc(ByRef arena, LocalVertex, 4)
  Dim vSpan As Span(Of LocalVertex) = SpanFrom(Of LocalVertex)(rawV, 4)
  Assert SpanSet(vSpan, 0, LocalVertex(10, 1.5)), "SpanSet inferred LocalVertex"
  Assert SpanSet(vSpan, 3, LocalVertex(40, 4.5)), "SpanSet index 3"
  Assert Not SpanSet(vSpan, 4, LocalVertex(99, 9.0)), "SpanSet OOB returns False"

  Dim gotV As LocalVertex
  Assert SpanGet(vSpan, 3, ByRef gotV) And (gotV.id = 40) And (gotV.weight = 4.5), "SpanGet inferred LocalVertex"
  Assert Not SpanGet(vSpan, -1, ByRef gotV), "SpanGet negative OOB"

  Dim subV As Span(Of LocalVertex) = SpanSub(vSpan, 2, 10)
  Assert subV.length = 2, "SpanSub clamps length"
  Dim atV As LocalVertex = SpanAt(subV, 1)
  Assert (atV.id = 40) And (atV.weight = 4.5), "SpanAt on sub-span"

  ' Vec(Of LocalVertex) & Vec(Of Double)
  Dim vv As Vec(Of LocalVertex) = VecNew(Of LocalVertex)(ByRef arena, 4)
  Assert VecPush(ByRef vv, LocalVertex(1, 0.5))
  Assert VecPush(ByRef vv, LocalVertex(2, 1.5))
  Assert vv.length = 2
  Assert VecGet(ByRef vv, 1).id = 2
  Assert VecSet(ByRef vv, 0, LocalVertex(9, 9.5))
  Dim poppedV As LocalVertex
  Assert VecPop(ByRef vv, ByRef poppedV) And (poppedV.id = 2)
  Assert VecGet(ByRef vv, 0).id = 9
  VecClear(ByRef vv)
  Assert vv.length = 0

  Dim vd As Vec(Of Double) = VecNew(Of Double)(ByRef arena, 3)
  Assert VecPush(ByRef vd, 1.25) And VecPush(ByRef vd, 2.75) And VecPush(ByRef vd, 4.0)
  Assert Not VecPush(ByRef vd, 5.0), "Vec full returns False"
  Assert (VecGet(ByRef vd, 0) + VecGet(ByRef vd, 1) + VecGet(ByRef vd, 2)) = 8.0

  ' Ring(Of Pair(Of Integer, Byte))
  Dim rg As Ring(Of Pair(Of Integer, Byte)) = RingNew(Of Pair(Of Integer, Byte))(ByRef arena, 2)
  Assert RingIsEmpty(ByRef rg)
  Assert RingPush(ByRef rg, Pair(Of Integer, Byte)(10, Byte(1)))
  Assert RingPush(ByRef rg, Pair(Of Integer, Byte)(20, Byte(2)))
  Assert RingIsFull(ByRef rg) And (RingLen(ByRef rg) = 2)
  Assert Not RingPush(ByRef rg, Pair(Of Integer, Byte)(30, Byte(3)))
  Dim pOut As Pair(Of Integer, Byte)
  Assert RingPop(ByRef rg, ByRef pOut) And (pOut.first = 10) And (Integer(pOut.second) = 1)
  Assert RingPush(ByRef rg, Pair(Of Integer, Byte)(30, Byte(3)))
  Assert RingPop(ByRef rg, ByRef pOut) And (pOut.first = 20)
  Assert RingPop(ByRef rg, ByRef pOut) And (pOut.first = 30)
  Assert RingIsEmpty(ByRef rg)

  ArenaDestroy(ByRef arena)
End Test

Test "Generics compile-time validation & freestanding arm-m cross-compilation"
  Dim p(256), modP(256) As Byte
  Dim pid As Integer = getpid()
  snprintf(p, 256, "%s/vb_gen_err_%d.vb", GetTempDir(), pid)
  snprintf(modP, 256, "%s/vb_gen_priv_mod_%d.vb", GetTempDir(), pid)

  ' 1. Wrong arity on generic struct
  Assert WriteTextFile(p, "Public Structure Box(Of T)\n  Dim v As T\nEnd Structure\nPublic Function main() As Integer\n  Dim b As Box(Of Integer, Single)\n  Return 0\nEnd Function\n")
  Assert RunVb(p, "--emit-only") <> 0, "Wrong generic arity must fail"
  Assert OutHas("expects 1 type argument(s), got 2"), "Arity error message"

  ' 2. Missing (Of ...) on generic struct type
  Assert WriteTextFile(p, "Public Structure Box(Of T)\n  Dim v As T\nEnd Structure\nPublic Function main() As Integer\n  Dim b As Box\n  Return 0\nEnd Function\n")
  Assert RunVb(p, "--emit-only") <> 0, "Missing (Of ...) on generic struct must fail"
  Assert OutHas("requires type arguments (Of ...)"), "Missing type args error message"

  ' 3. Uninferred type parameter
  Assert WriteTextFile(p, "Public Function ZeroOf(Of T)() As T\n  Dim z As T\n  Return z\nEnd Function\nPublic Function main() As Integer\n  Dim x As Integer = ZeroOf()\n  Return x\nEnd Function\n")
  Assert RunVb(p, "--emit-only") <> 0, "Uninferred generic function call must fail"
  Assert OutHas("Cannot infer type parameter 'T'"), "Inference error message"

  ' 4. Duplicate type parameter
  Assert WriteTextFile(p, "Public Structure Dup(Of T, T)\n  Dim a As T\nEnd Structure\nPublic Function main() As Integer\n  Return 0\nEnd Function\n")
  Assert RunVb(p, "--emit-only") <> 0, "Duplicate type param must fail"
  Assert OutHas("Duplicate type parameter 'T'"), "Duplicate type param message"

  ' 5. Infinite recursive generic instantiation depth guard
  Assert WriteTextFile(p, "Public Structure Box(Of T)\n  Dim v As T\nEnd Structure\nPublic Function Inf(Of T)(x As T) As Integer\n  Return Inf(Of Box(Of T))(Box(Of T)(x))\nEnd Function\nPublic Function main() As Integer\n  Return Inf(Of Integer)(1)\nEnd Function\n")
  Assert RunVb(p, "--emit-only") <> 0, "Infinite generic instantiation must be caught"
  Assert OutHas("Maximum generic instantiation depth exceeded"), "Max depth message"

  ' 6. Private generic template isolation across modules
  Assert WriteTextFile(modP, "Private Structure SecretBox(Of T)\n  Dim v As T\nEnd Structure\n")
  Dim incSrc(512) As Byte
  snprintf(incSrc, 512, "Include \"%s\"\nPublic Function main() As Integer\n  Dim b As SecretBox(Of Integer)\n  Return 0\nEnd Function\n", modP)
  Assert WriteTextFile(p, incSrc)
  Assert RunVb(p, "--emit-only") <> 0, "Private generic struct from included module must fail"
  Assert OutHas("Cannot instantiate Private generic structure"), "Private generic error message"
  unlink(modP)

  ' 7. Freestanding arm-m cross-compilation with generic Span(Of Integer) & --ast printing
  Assert WriteTextFile(p, "Include \"std/span.vb\"\nPublic Function SumArm(buf As Pointer, n As Long) As Integer\n  Dim s As Span(Of Integer) = SpanFrom(Of Integer)(buf, n)\n  Dim acc As Integer = 0\n  Dim v As Integer = 0\n  For i = 0 To 3 Do\n    If SpanGet(s, Long(i), ByRef v) Then\n      acc = acc + v\n    End If\n  End For\n  Return acc\nEnd Function\n")
  Dim rcArm As Integer = RunVb(p, "--target arm-m --ast --ir --emit-only")
  Assert rcArm = 0, "Generic Span(Of Integer) must cross-compile cleanly to arm-m"
  Assert OutHas("Span(Of Integer)"), "--ast must pretty-print generic type args"
  Assert OutHas("thumbv7em-unknown-none-eabi"), "arm-m target triple"
  Assert OutHas("Span__Of_Integer_End"), "Monomorphized Span struct in arm-m IR"

  ' 8. Generic function inherits strict --max-stack enforcement (Byte passes, Integer exceeds 64 KB)
  Assert WriteTextFile(p, "Public Function BufWork(Of T)() As Integer\n  Dim buf(20000) As T\n  Return Len(buf)\nEnd Function\nPublic Function main() As Integer\n  Return BufWork(Of Byte)()\nEnd Function\n")
  Assert RunVb(p, "--emit-only") = 0, "20000 x Byte = 20 KB fits under 64 KB stack limit"
  Assert WriteTextFile(p, "Public Function BufWork(Of T)() As Integer\n  Dim buf(20000) As T\n  Return Len(buf)\nEnd Function\nPublic Function main() As Integer\n  Return BufWork(Of Integer)()\nEnd Function\n")
  Dim rcStk As Integer = RunVb(p, "--emit-only")
  unlink(p)
  Assert rcStk <> 0, "20000 x Integer = 80 KB must fail strict stack limit after monomorphization"
  Assert OutHas("exceeding max stack limit"), "Stack limit error on monomorphized generic"
End Test
