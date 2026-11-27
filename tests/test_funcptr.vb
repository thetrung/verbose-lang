' VerBose Test Suite: Milestone 4 — Typed Function Pointers (FuncPtr / SubPtr)

Include "harness.vb"
Include "../std/result.vb"

Declare Function abs(x As Integer) As Integer
Declare Function strlen(s As Pointer) As Long
Declare Sub qsort(base As Pointer, nmemb As Long, size As Long, cmp As FuncPtr(a As Pointer, b As Pointer) As Integer)

Structure FpVec2
  Dim x, y As Integer
End Structure

Structure FpVTable
  Dim onPing As SubPtr()
  Dim onUnary As FuncPtr(x As Integer) As Integer
  Dim onBinary As FuncPtr(a, b As Integer) As Integer
  Dim onMutate As SubPtr(ByRef dst As Integer, delta As Integer)
  Dim onMakeVec As FuncPtr(x As Integer, y As Integer) As FpVec2
End Structure

Structure FpHandler(Of T)
  Dim cb As FuncPtr(T) As Integer
End Structure

Private Static g_pingCount As Integer = 0

Private Sub DoPing()
  g_pingCount = g_pingCount + 1
End Sub

Private Function Square(x As Integer) As Integer
  Return x * x
End Function

Private Function AddOne(x As Integer) As Integer
  Return x + 1
End Function

Private Function AddInts(a, b As Integer) As Integer
  Return a + b
End Function

Private Function MulInts(a, b As Integer) As Integer
  Return a * b
End Function

Private Sub BumpByRef(ByRef dst As Integer, delta As Integer)
  dst = dst + delta
End Sub

Private Function MakeFpVec(x, y As Integer) As FpVec2
  Return FpVec2 With ( x = x, y = y )
End Function

Private Function SumVec(v As FpVec2) As Integer
  Return v.x + v.y
End Function

Private Function SumSlice(arr() As Integer, n As Integer) As Integer
  Dim s As Integer = 0
  For i = 0 To n - 1 Do
    s = s + arr(i)
  End For
  Return s
End Function

Private Function UnwrapOrZero(opt As Option(Of Integer)) As Integer
  Match opt With
    Case Some(v) Then Return v
    Case None Then Return 0
  End Match
End Function

Private Function ChooseBinOp(useMul As Boolean) As FuncPtr(Integer, Integer) As Integer
  If useMul Then
    Return AddressOf(MulInts)
  End If
  Return AddressOf(AddInts)
End Function

Private Function ApplyTwice(f As FuncPtr(Integer) As Integer, x As Integer) As Integer
  Return f(f(x))
End Function

Private Sub SwapFn(ByRef slot As FuncPtr(Integer) As Integer, nextFn As FuncPtr(Integer) As Integer)
  slot = nextFn
End Sub

Private Function HigherOrder(combiner As FuncPtr(f As FuncPtr(Integer) As Integer, x As Integer) As Integer, inner As FuncPtr(Integer) As Integer, v As Integer) As Integer
  Return combiner(inner, v)
End Function

Private Function GetVTable() As FpVTable
  Return FpVTable With (
    onPing = AddressOf(DoPing),
    onUnary = AddressOf(Square),
    onBinary = AddressOf(AddInts),
    onMutate = AddressOf(BumpByRef),
    onMakeVec = AddressOf(MakeFpVec)
  )
End Function

Private Function GenericApply(Of T, R)(x As T, f As FuncPtr(T) As R) As R
  Return f(x)
End Function

Private Function GenericId(Of T)(x As T) As T
  Return x
End Function

Private Function DoubleToInt(d As Double) As Integer
  Return Integer(d * 2.0)
End Function

Private Async Function AsyncPickOp() As FuncPtr(Integer) As Integer
  Return AddressOf(Square)
End Function

Private Function FpCompareInts(a As Pointer, b As Pointer) As Integer
  Dim va As Integer = PtrRead(a, Integer)
  Dim vb As Integer = PtrRead(b, Integer)
  Return va - vb
End Function

Private Static g_staticOp As FuncPtr(Integer) As Integer = AddressOf(Square)
Private Static g_nullOp As FuncPtr(Integer) As Integer = 0
Private Static g_opTable(2) As FuncPtr(Integer) As Integer = (AddressOf(AddOne), AddressOf(Square))

Test "Basic FuncPtr/SubPtr variables, Static const-init, null checks, comparisons, casts, and higher-order calls"
  Assert SizeOf(FuncPtr(Integer) As Integer) = SizeOf(Pointer), "FuncPtr size matches Pointer"
  Assert SizeOf(SubPtr()) = SizeOf(Pointer), "SubPtr size matches Pointer"
  Assert IsNull(g_nullOp), "0-initialized Static FuncPtr is null"
  Assert g_nullOp = 0, "FuncPtr = 0"
  Assert Not IsNull(g_staticOp), "Static AddressOf(fn) is non-null"
  Assert g_staticOp <> 0, "FuncPtr <> 0"
  Assert Boolean(g_staticOp), "Boolean(non-null FuncPtr) is True"
  Assert Not Boolean(g_nullOp), "Boolean(null FuncPtr) is False"
  Assert g_staticOp(6) = 36, "Static FuncPtr call"
  Assert g_opTable(0)(9) = 10, "Static FuncPtr array elem 0 call"
  Assert g_opTable(1)(5) = 25, "Static FuncPtr array elem 1 call"

  Dim f As FuncPtr(x As Integer) As Integer = AddressOf(Square)
  Dim f2 As FuncPtr(Integer) As Integer = AddressOf(Square)
  Assert f = f2, "equal FuncPtr values compare equal"
  Assert f(7) = 49, "local FuncPtr call"

  Dim rawP As Pointer = f
  Assert Not IsNull(rawP), "FuncPtr widens safely to Pointer"
  Assert Pointer(f) = rawP, "explicit Pointer(fp) cast matches"

  SwapFn(ByRef f, AddressOf(AddOne))
  Assert f <> f2, "swapped FuncPtr differs"
  Assert f(7) = 8, "ByRef FuncPtr update took effect"

  Assert ApplyTwice(AddressOf(AddOne), 10) = 12, "FuncPtr passed as parameter"
  Assert HigherOrder(AddressOf(ApplyTwice), AddressOf(Square), 3) = 81, "nested FuncPtr parameter"
  Assert ChooseBinOp(False)(4, 5) = 9, "calling returned FuncPtr (add)"
  Assert ChooseBinOp(True)(4, 5) = 20, "calling returned FuncPtr (mul)"

  ' Direct call on AddressOf expression
  Assert AddressOf(Square)(8) = 64, "calling AddressOf(fn) directly"
End Test

Test "Struct fields, With blocks, arrays/slices, ByRef, structs, and unions through FuncPtr/SubPtr"
  g_pingCount = 0
  Dim vt As FpVTable = GetVTable()
  vt.onPing()
  Assert g_pingCount = 1, "0-arg SubPtr struct field call"
  Assert vt.onUnary(5) = 25, "1-arg FuncPtr struct field call"
  Assert vt.onBinary(12, 30) = 42, "2-arg FuncPtr struct field call"

  Dim acc As Integer = 10
  vt.onMutate(ByRef acc, 7)
  Assert acc = 17, "ByRef SubPtr struct field call"

  Dim v As FpVec2 = vt.onMakeVec(3, 4)
  Assert (v.x = 3) And (v.y = 4), "struct return from FuncPtr field"
  Assert vt.onMakeVec(9, 11).y = 11, "chained rvalue member access on FuncPtr call result"
  Assert GetVTable().onBinary(100, 23) = 123, "rvalue struct FuncPtr field call"

  With vt
    .onPing()
    Assert .onUnary(4) = 16, "With .onUnary(4)"
    Assert .onBinary(6, 7) = 13, "With .onBinary(6, 7)"
    .onMutate(ByRef acc, 3)
  End With
  Assert g_pingCount = 2, "With .onPing() ran"
  Assert acc = 20, "With .onMutate(ByRef acc, 3) ran"

  ' Struct & slice & union parameters via FuncPtr
  Dim fSumVec As FuncPtr(v As FpVec2) As Integer = AddressOf(SumVec)
  Assert fSumVec(v) = 7, "ByVal Structure param via FuncPtr"

  Dim nums(4) As Integer = (2, 4, 6, 8)
  Dim fSumArr As FuncPtr(arr() As Integer, n As Integer) As Integer = AddressOf(SumSlice)
  Assert fSumArr(nums, 4) = 20, "unsized slice param via FuncPtr"

  Dim fOpt As FuncPtr(Option(Of Integer)) As Integer = AddressOf(UnwrapOrZero)
  Assert fOpt(OptSome(99)) = 99, "Union param Some via FuncPtr"
  Assert fOpt(OptNone(Of Integer)()) = 0, "Union param None via FuncPtr"

  ' Arena-allocated slice of FuncPtrs
  Dim a As Arena = ArenaCreate(1024)
  Defer ArenaDestroy(ByRef a)
  Dim dyn() As FuncPtr(Integer, Integer) As Integer = ArenaAlloc(ByRef a, FuncPtr(Integer, Integer) As Integer, 2)
  dyn(0) = AddressOf(AddInts)
  dyn(1) = AddressOf(MulInts)
  Assert dyn(0)(3, 4) = 7, "Arena slice FuncPtr 0"
  Assert dyn(1)(3, 4) = 12, "Arena slice FuncPtr 1"
End Test

Test "Generics (inference & struct fields), Async Task(FuncPtr), and C FFI callbacks"
  ' Generic function type inference through FuncPtr parameter
  Assert GenericApply(7, AddressOf(Square)) = 49, "GenericApply infers T=Integer, R=Integer"
  Assert GenericApply(3.5, AddressOf(DoubleToInt)) = 7, "GenericApply infers T=Double, R=Integer"
  Assert GenericApply(19, AddressOf(GenericId(Of Integer))) = 19, "AddressOf(GenericId(Of Integer)) passed to GenericApply"

  ' Generic struct holding FuncPtr(T) As Integer
  Dim h As FpHandler(Of Double) = FpHandler(Of Double) With ( cb = AddressOf(DoubleToInt) )
  Assert h.cb(4.5) = 9, "generic struct field FuncPtr(T)"

  ' Async Function returning FuncPtr
  Dim job As Task(FuncPtr(Integer) As Integer) = AsyncPickOp()
  Dim picked As FuncPtr(Integer) As Integer = Await job
  Assert picked(9) = 81, "Await Task(FuncPtr) returns callable FuncPtr"

  ' Calling C Declare functions via typed FuncPtr
  Dim cAbs As FuncPtr(Integer) As Integer = AddressOf(abs)
  Assert cAbs(-123) = 123, "C Declare abs via FuncPtr"
  Dim cStrlen As FuncPtr(Pointer) As Long = AddressOf(strlen)
  Assert cStrlen("VerBose") = 7, "C Declare strlen via FuncPtr"

  ' Passing typed FuncPtr callback to C qsort
  Dim arr(5) As Integer = (40, 10, 50, 20, 30)
  Dim cmp As FuncPtr(Pointer, Pointer) As Integer = AddressOf(FpCompareInts)
  qsort(AddressOf(arr(0)), 5, SizeOf(Integer), cmp)
  Assert (arr(0) = 10) And (arr(1) = 20) And (arr(2) = 30) And (arr(3) = 40) And (arr(4) = 50), "typed FuncPtr callback with C qsort"
End Test

Test "C ABI struct wrapper for extern C FuncPtr, Type Analyzer errors, --ast, and arm-m"
  Dim comp(768), tmpDir(256), cPath(300), srcPath(300), objPath(300) As Byte
  GetCompilerAbs(comp, 768)
  Dim pid As Integer = getpid()
  snprintf(tmpDir, 256, "/tmp/vb_fp_%d", pid)
  snprintf(cPath, 300, "%s/c_fp_abi.c", tmpDir)
  snprintf(srcPath, 300, "%s/case.vb", tmpDir)
  snprintf(objPath, 300, "%s/arm.o", tmpDir)
  Dim cmd(1536) As Byte
  snprintf(cmd, 1536, "mkdir -p '%s'", tmpDir)
  system(cmd)

  ' 1. C ABI ByVal struct extern function called through typed FuncPtr
  WriteTextFile(cPath, "typedef struct { float x; float y; } CVec2;\nCVec2 c_vec2_add(CVec2 a, CVec2 b) { CVec2 r = { a.x + b.x, a.y + b.y }; return r; }\n")
  WriteTextFile(srcPath, "Structure CVec2\n  Dim x, y As Single\nEnd Structure\nDeclare Function c_vec2_add(a As CVec2, b As CVec2) As CVec2\nPublic Function main() As Integer\n  Dim f As FuncPtr(CVec2, CVec2) As CVec2 = AddressOf(c_vec2_add)\n  Dim r As CVec2 = f(CVec2(1.5, 2.5), CVec2(3.5, 4.5))\n  If (r.x = 5.0) And (r.y = 7.0) Then\n    Return 0\n  End If\n  Return 1\nEnd Function\n")
  snprintf(cmd, 1536, "'%s' '%s' '%s'", comp, srcPath, cPath)
  Assert RunShell(cmd) = 0, "extern C function with ByVal struct params/return works via FuncPtr"

  ' 2. Signature mismatch (param type)
  WriteTextFile(srcPath, "Private Function F(x As Long) As Integer\n  Return Integer(x)\nEnd Function\nPublic Function main() As Integer\n  Dim p As FuncPtr(Integer) As Integer = AddressOf(F)\n  Return 0\nEnd Function\n")
  snprintf(cmd, 1536, "'%s' '%s' --emit-only", comp, srcPath)
  Assert RunShell(cmd) <> 0, "mismatched FuncPtr param type must fail"

  ' 3. FuncPtr vs SubPtr mismatch
  WriteTextFile(srcPath, "Private Sub S(x As Integer)\nEnd Sub\nPublic Function main() As Integer\n  Dim p As FuncPtr(Integer) As Integer = AddressOf(S)\n  Return 0\nEnd Function\n")
  snprintf(cmd, 1536, "'%s' '%s' --emit-only", comp, srcPath)
  Assert RunShell(cmd) <> 0, "assigning SubPtr to FuncPtr must fail"

  ' 4. ByRef vs ByVal signature mismatch
  WriteTextFile(srcPath, "Private Sub S(ByRef x As Integer)\nEnd Sub\nPublic Function main() As Integer\n  Dim p As SubPtr(Integer) = AddressOf(S)\n  Return 0\nEnd Function\n")
  snprintf(cmd, 1536, "'%s' '%s' --emit-only", comp, srcPath)
  Assert RunShell(cmd) <> 0, "ByRef vs ByVal FuncPtr mismatch must fail"

  ' 5. Untyped Pointer cannot implicitly convert to FuncPtr
  WriteTextFile(srcPath, "Public Function main() As Integer\n  Dim raw As Pointer = 0\n  Dim p As FuncPtr(Integer) As Integer = raw\n  Return 0\nEnd Function\n")
  snprintf(cmd, 1536, "'%s' '%s' --emit-only", comp, srcPath)
  Assert RunShell(cmd) <> 0, "untyped Pointer cannot implicitly assign to FuncPtr"

  ' 6. Cannot take AddressOf an Async function
  WriteTextFile(srcPath, "Private Async Function Work() As Integer\n  Return 1\nEnd Function\nPublic Function main() As Integer\n  Dim p As Pointer = AddressOf(Work)\n  Return 0\nEnd Function\n")
  snprintf(cmd, 1536, "'%s' '%s' --emit-only", comp, srcPath)
  Assert RunShell(cmd) <> 0, "AddressOf(Async) must fail"
  Assert OutHas("Cannot take AddressOf an Async function"), "error reports Async AddressOf"

  ' 7. Calling non-FuncPtr expression
  WriteTextFile(srcPath, "Public Function main() As Integer\n  Dim x As Integer = 5\n  Dim y As Integer = (x)(1, 2)\n  Return y\nEnd Function\n")
  snprintf(cmd, 1536, "'%s' '%s' --emit-only", comp, srcPath)
  Assert RunShell(cmd) <> 0, "invoking non-FuncPtr must fail"
  Assert OutHas("not a function pointer"), "error reports non-function-pointer call"

  ' 8. --ast prints FuncPtr & SubPtr, and arm-m compiles freestanding
  WriteTextFile(srcPath, "Private Function Inc(x As Integer) As Integer\n  Return x + 1\nEnd Function\nPrivate Sub Nop()\nEnd Sub\nPublic Function main() As Integer\n  Dim f As FuncPtr(x As Integer) As Integer = AddressOf(Inc)\n  Dim s As SubPtr() = AddressOf(Nop)\n  s()\n  Return f(41)\nEnd Function\n")
  snprintf(cmd, 1536, "'%s' --ast --emit-only '%s'", comp, srcPath)
  Assert RunShell(cmd) = 0, "--ast on FuncPtr/SubPtr succeeds"
  Assert OutHas("FuncPtr(x As Integer) As Integer"), "--ast prints FuncPtr"
  Assert OutHas("SubPtr()"), "--ast prints SubPtr"

  snprintf(cmd, 1536, "'%s' --target arm-m -c '%s' -o '%s'", comp, srcPath, objPath)
  Assert RunShell(cmd) = 0, "arm-m cross-compile with FuncPtr/SubPtr succeeds"

  snprintf(cmd, 1536, "rm -rf '%s'", tmpDir)
  system(cmd)
End Test
