' Target presets, cross-compilation, Volatile/MMIO, and standard library integration tests.

Include "harness.vb"

Private Function CheckObjHeader(path As Pointer, b0, b1, b2, b3, offM, m0, m1 As Integer) As Boolean
  Dim fp As Pointer = Fopen(path, "rb")
  If IsNull(fp) Then
    Return False
  End If
  Dim hdr(32) As Byte
  Dim n As Long = fread(hdr, 1, 32, fp)
  fclose(fp)
  If n < 24 Then
    Return False
  End If
  Dim h0 As Integer = hdr(0), h1 As Integer = hdr(1), h2 As Integer = hdr(2), h3 As Integer = hdr(3)
  If ((h0 And 255) <> b0) Or ((h1 And 255) <> b1) Then
    Return False
  End If
  If (b2 >= 0) And ((h2 And 255) <> b2) Then
    Return False
  End If
  If (b3 >= 0) And ((h3 And 255) <> b3) Then
    Return False
  End If
  If offM >= 0 Then
    Dim hm0 As Integer = hdr(offM), hm1 As Integer = hdr(offM + 1)
    If ((hm0 And 255) <> m0) Or ((hm1 And 255) <> m1) Then
      Return False
    End If
  End If
  Return True
End Function

Test "targets: linux-x86_64 explicit IR and host native zero-warning compile & run"
  Dim comp(768), tmpDir(256), srcPath(300), llPath(300), binPath(300) As Byte
  GetCompilerAbs(comp, 768)
  Dim pid As Integer = getpid()
  snprintf(tmpDir, 256, "/tmp/vb_tgt_%d_linux", pid)
  snprintf(srcPath, 300, "%s/hello.vb", tmpDir)
  snprintf(llPath, 300, "%s/hello.ll", tmpDir)
  snprintf(binPath, 300, "%s/hello", tmpDir)

  Dim mkCmd(512) As Byte
  snprintf(mkCmd, 512, "mkdir -p '%s'", tmpDir)
  system(mkCmd)

  WriteTextFile(srcPath, "Public Function main() As Integer\n  Printf(\"hello host\\n\")\n  Return 0\nEnd Function\n")

  Dim cmd(1536) As Byte
  ' 1. Verify --target linux-x86_64 --emit-only emits correct datalayout and triple
  snprintf(cmd, 1536, "cd '%s' && '%s' hello.vb --target linux-x86_64 --emit-only -o hello.ll", tmpDir, comp)
  Assert RunShell(cmd) = 0
  Assert FileExists(llPath)
  Assert FileHas(llPath, "target datalayout = \"e-m:e-p270:32:32-p271:32:32-p272:64:64-i64:64-i128:128-f80:128-n8:16:32:64-S128\"")
  Assert FileHas(llPath, "target triple = \"x86_64-pc-linux-gnu\"")

  ' 2. Verify default host target compilation with clang produces ZERO warnings
  snprintf(cmd, 1536, "cd '%s' && '%s' hello.vb -o hello", tmpDir, comp)
  Assert RunShell(cmd) = 0
  Assert Not OutHas("-Woverride-module")
  Assert Not OutHas("warning:")
  Assert FileExists(binPath)

  ' 3. Verify execution works on current host
  snprintf(cmd, 1536, "'%s'", binPath)
  Assert RunShell(cmd) = 0
  Assert OutHas("hello host")

  snprintf(mkCmd, 512, "rm -rf '%s'", tmpDir)
  system(mkCmd)
End Test

Test "targets: linux-arm64 --emit-only and -c produces valid AArch64 ELF .o"
  Dim comp(768), tmpDir(256), srcPath(300), llPath(300), objPath(300) As Byte
  GetCompilerAbs(comp, 768)
  Dim pid As Integer = getpid()
  snprintf(tmpDir, 256, "/tmp/vb_tgt_%d_arm64", pid)
  snprintf(srcPath, 300, "%s/arm64.vb", tmpDir)
  snprintf(llPath, 300, "%s/arm64.ll", tmpDir)
  snprintf(objPath, 300, "%s/arm64.o", tmpDir)

  Dim mkCmd(512) As Byte
  snprintf(mkCmd, 512, "mkdir -p '%s'", tmpDir)
  system(mkCmd)

  WriteTextFile(srcPath, "Public Function main() As Integer\n  Return 7\nEnd Function\n")

  Dim cmd(1536) As Byte
  snprintf(cmd, 1536, "cd '%s' && '%s' arm64.vb --target linux-arm64 --emit-only -o arm64.ll", tmpDir, comp)
  Assert RunShell(cmd) = 0
  Assert FileExists(llPath)
  Assert FileHas(llPath, "target datalayout = \"e-m:e-i8:8:32-i16:16:32-i64:64-i128:128-n32:64-S128\"")
  Assert FileHas(llPath, "target triple = \"aarch64-unknown-linux-gnu\"")

  snprintf(cmd, 1536, "cd '%s' && '%s' arm64.vb --target linux-arm64 -c -o arm64.o", tmpDir, comp)
  Assert RunShell(cmd) = 0
  Assert FileExists(objPath)
  ' ELF 64-bit little-endian, e_machine = 0x00b7 (EM_AARCH64) at offset 18
  Assert CheckObjHeader(objPath, 127, 69, 76, 70, 18, 183, 0)

  snprintf(mkCmd, 512, "rm -rf '%s'", tmpDir)
  system(mkCmd)
End Test

Test "targets: macos-arm64 --emit-only and -c produces valid Mach-O .o"
  Dim comp(768), tmpDir(256), srcPath(300), llPath(300), objPath(300) As Byte
  GetCompilerAbs(comp, 768)
  Dim pid As Integer = getpid()
  snprintf(tmpDir, 256, "/tmp/vb_tgt_%d_mac", pid)
  snprintf(srcPath, 300, "%s/mac.vb", tmpDir)
  snprintf(llPath, 300, "%s/mac.ll", tmpDir)
  snprintf(objPath, 300, "%s/mac.o", tmpDir)

  Dim mkCmd(512) As Byte
  snprintf(mkCmd, 512, "mkdir -p '%s'", tmpDir)
  system(mkCmd)

  WriteTextFile(srcPath, "Public Function main() As Integer\n  Return 42\nEnd Function\n")

  Dim cmd(1536) As Byte
  ' 1. macos-arm64 --emit-only
  snprintf(cmd, 1536, "cd '%s' && '%s' mac.vb --target macos-arm64 --emit-only -o mac.ll", tmpDir, comp)
  Assert RunShell(cmd) = 0
  Assert FileExists(llPath)
  Assert FileHas(llPath, "target datalayout = \"e-m:o-p270:32:32-p271:32:32-p272:64:64-i64:64-i128:128-n32:64-S128\"")
  Assert FileHas(llPath, "target triple = \"arm64-apple-macosx11.0.0\"")

  ' 2. macos-arm64 -c produces Mach-O 64-bit arm64 object (MH_MAGIC_64 = cf fa ed fe, cputype = 0x0100000c)
  snprintf(cmd, 1536, "cd '%s' && '%s' mac.vb --target macos-arm64 -c -o mac.o", tmpDir, comp)
  Assert RunShell(cmd) = 0
  Assert FileExists(objPath)
  Assert CheckObjHeader(objPath, 207, 250, 237, 254, 4, 12, 0)

  snprintf(mkCmd, 512, "rm -rf '%s'", tmpDir)
  system(mkCmd)
End Test

Test "targets: windows-x86_64 --emit-only and -c produces valid COFF x86-64 .o"
  Dim comp(768), tmpDir(256), srcPath(300), llPath(300), objPath(300) As Byte
  GetCompilerAbs(comp, 768)
  Dim pid As Integer = getpid()
  snprintf(tmpDir, 256, "/tmp/vb_tgt_%d_win", pid)
  snprintf(srcPath, 300, "%s/win.vb", tmpDir)
  snprintf(llPath, 300, "%s/win.ll", tmpDir)
  snprintf(objPath, 300, "%s/win.o", tmpDir)

  Dim mkCmd(512) As Byte
  snprintf(mkCmd, 512, "mkdir -p '%s'", tmpDir)
  system(mkCmd)

  WriteTextFile(srcPath, "Public Function main() As Integer\n  Return 9\nEnd Function\n")

  Dim cmd(1536) As Byte
  snprintf(cmd, 1536, "cd '%s' && '%s' win.vb --target windows-x86_64 --emit-only -o win.ll", tmpDir, comp)
  Assert RunShell(cmd) = 0
  Assert FileExists(llPath)
  Assert FileHas(llPath, "target datalayout = \"e-m:w-p270:32:32-p271:32:32-p272:64:64-i64:64-i128:128-f80:128-n8:16:32:64-S128\"")
  Assert FileHas(llPath, "target triple = \"x86_64-w64-windows-gnu\"")

  snprintf(cmd, 1536, "cd '%s' && '%s' win.vb --target windows-x86_64 -c -o win.o", tmpDir, comp)
  Assert RunShell(cmd) = 0
  Assert FileExists(objPath)
  ' COFF IMAGE_FILE_MACHINE_AMD64 = 0x8664 little-endian (0x64, 0x86)
  Assert CheckObjHeader(objPath, 100, 134, -1, -1, -1, 0, 0)

  snprintf(mkCmd, 512, "rm -rf '%s'", tmpDir)
  system(mkCmd)
End Test

Test "targets: arm-m --emit-only and -c produces freestanding Thumb ELF .o"
  Dim comp(768), tmpDir(256), srcPath(300), llPath(300), objPath(300) As Byte
  GetCompilerAbs(comp, 768)
  Dim pid As Integer = getpid()
  snprintf(tmpDir, 256, "/tmp/vb_tgt_%d_armm", pid)
  snprintf(srcPath, 300, "%s/cortex.vb", tmpDir)
  snprintf(llPath, 300, "%s/cortex.ll", tmpDir)
  snprintf(objPath, 300, "%s/cortex.o", tmpDir)

  Dim mkCmd(512) As Byte
  snprintf(mkCmd, 512, "mkdir -p '%s'", tmpDir)
  system(mkCmd)

  WriteTextFile(srcPath, "Public Function main() As Integer\n  Dim sz As Integer = SizeOf(Pointer)\n  Return sz\nEnd Function\n")

  Dim cmd(1536) As Byte
  ' 1. arm-m --emit-only emits thumbv7em triple, p:32:32, SizeOf(Pointer) = 4, freestanding (no printf/fopen externs)
  snprintf(cmd, 1536, "cd '%s' && '%s' cortex.vb --target arm-m --emit-only -o cortex.ll", tmpDir, comp)
  Assert RunShell(cmd) = 0
  Assert FileExists(llPath)
  Assert FileHas(llPath, "target datalayout = \"e-m:e-p:32:32-Fi8-i64:64-v128:64:128-a:0:32-n32-S64\"")
  Assert FileHas(llPath, "target triple = \"thumbv7em-unknown-none-eabi\"")
  ' Check SizeOf(Pointer) constant folded to 4: %r2 = add i32 0, 4
  Assert FileHas(llPath, "add i32 0, 4")
  ' Freestanding: libc externs should NOT be declared
  Assert Not FileHas(llPath, "@printf")
  Assert Not FileHas(llPath, "@fopen")
  Assert Not FileHas(llPath, "@malloc")

  ' 2. arm-m -c produces ARM ELF relocatable object (ELF32, e_machine = 0x0028 EM_ARM)
  snprintf(cmd, 1536, "cd '%s' && '%s' cortex.vb --target arm-m -c -o cortex.o", tmpDir, comp)
  Assert RunShell(cmd) = 0
  Assert FileExists(objPath)
  Assert CheckObjHeader(objPath, 127, 69, 76, 70, 18, 40, 0)

  snprintf(mkCmd, 512, "rm -rf '%s'", tmpDir)
  system(mkCmd)
End Test

Test "volatile and mmio: Volatile Dim, Static, At addr lowering and runtime"
  Dim comp(768), tmpDir(256), srcPath(300), llPathLinux(300), llPathArm(300), binPath(300) As Byte
  GetCompilerAbs(comp, 768)
  Dim pid As Integer = getpid()
  snprintf(tmpDir, 256, "/tmp/vb_tgt_%d_mmio", pid)
  snprintf(srcPath, 300, "%s/mmio.vb", tmpDir)
  snprintf(llPathLinux, 300, "%s/mmio_linux.ll", tmpDir)
  snprintf(llPathArm, 300, "%s/mmio_arm.ll", tmpDir)
  snprintf(binPath, 300, "%s/mmio_run", tmpDir)

  Dim mkCmd(512) As Byte
  snprintf(mkCmd, 512, "mkdir -p '%s'", tmpDir)
  system(mkCmd)

  ' MMIO peripheral registers (0x40020014 and 0xE000E010)
  WriteTextFile(srcPath, "Public Volatile Static GPIOA_ODR As Integer At 0x40020014\nPublic Volatile Static SYS_CTRL As Integer At 0xE000E010\nPublic Volatile Static g_count As Integer = 10\n\nPublic Function main() As Integer\n  Volatile Dim localVal As Integer = 5\n  localVal = localVal + 1\n  Dim x As Integer = GPIOA_ODR\n  SYS_CTRL = x + localVal\n  g_count = g_count + 1\n\n  Dim p As Pointer = HeapAlloc(Integer, 1)\n  VolatileStore(p, Integer, 999)\n  Dim loaded As Integer = VolatileLoad(p, Integer)\n  HeapFree(p)\n\n  If loaded <> 999 Then\n    Return 1\n  End If\n  If g_count <> 11 Then\n    Return 2\n  End If\n  If localVal <> 6 Then\n    Return 3\n  End If\n  Printf(\"volatile ok: %d\\n\", loaded)\n  Return 0\nEnd Function\n")

  Dim cmd(1536) As Byte
  ' 1. Verify Linux IR lowering
  snprintf(cmd, 1536, "cd '%s' && '%s' mmio.vb --emit-only -o mmio_linux.ll", tmpDir, comp)
  Assert RunShell(cmd) = 0
  Assert FileExists(llPathLinux)
  ' Fixed-address At lowering to inttoptr + load volatile / store volatile
  Assert FileHas(llPathLinux, "inttoptr i64 1073872916 to ptr")
  Assert FileHas(llPathLinux, "inttoptr i64 3758153744 to ptr")
  Assert FileHas(llPathLinux, "load volatile")
  Assert FileHas(llPathLinux, "store volatile")

  ' 2. Verify arm-m IR lowering (32-bit inttoptr i32)
  snprintf(cmd, 1536, "cd '%s' && '%s' mmio.vb --target arm-m --emit-only -o mmio_arm.ll", tmpDir, comp)
  Assert RunShell(cmd) = 0
  Assert FileExists(llPathArm)
  Assert FileHas(llPathArm, "inttoptr i32 1073872916 to ptr")
  Assert FileHas(llPathArm, "inttoptr i32 3758153744 to ptr")
  Assert FileHas(llPathArm, "load volatile")
  Assert FileHas(llPathArm, "store volatile")

  ' 3. Runtime execution test on host linux-x86_64 (running main with HeapAlloc, VolatileLoad, VolatileStore, Volatile Dim, Volatile Static)
  ' Note: we test runtime with a file that doesn't actually dereference the hardware addresses 0x40020014 to avoid segfaulting host
  Dim rtSrc(300) As Byte
  snprintf(rtSrc, 300, "%s/rt.vb", tmpDir)
  WriteTextFile(rtSrc, "Public Volatile Static g_val As Integer = 100\nPublic Function main() As Integer\n  Volatile Dim v As Integer = 50\n  v = v + 10\n  g_val = g_val + v\n  Dim p As Pointer = HeapAlloc(Integer, 1)\n  VolatileStore(p, Integer, 777)\n  Dim loaded As Integer = VolatileLoad(p, Integer)\n  HeapFree(p)\n  Printf(\"val=%d loaded=%d\\n\", g_val, loaded)\n  If g_val = 160 Then\n    If loaded = 777 Then\n      Return 0\n    End If\n  End If\n  Return 1\nEnd Function\n")

  snprintf(cmd, 1536, "cd '%s' && '%s' rt.vb -o rt_bin && ./rt_bin", tmpDir, comp)
  Assert RunShell(cmd) = 0
  Assert OutHas("val=160 loaded=777")

  snprintf(mkCmd, 512, "rm -rf '%s'", tmpDir)
  system(mkCmd)
End Test

Test "negative checks: target, At syntax and VolatileLoad/Store errors"
  Dim comp(768), tmpDir(256), srcPath(300) As Byte
  GetCompilerAbs(comp, 768)
  Dim pid As Integer = getpid()
  snprintf(tmpDir, 256, "/tmp/vb_tgt_%d_neg", pid)
  snprintf(srcPath, 300, "%s/neg.vb", tmpDir)

  Dim mkCmd(512) As Byte
  snprintf(mkCmd, 512, "mkdir -p '%s'", tmpDir)
  system(mkCmd)

  Dim cmd(1536) As Byte

  ' 1. Unknown target
  WriteTextFile(srcPath, "Public Function main() As Integer\n  Return 0\nEnd Function\n")
  snprintf(cmd, 1536, "'%s' '%s' --target unknown-cpu --emit-only", comp, srcPath)
  Assert RunShell(cmd) <> 0
  Assert OutHas("unknown target 'unknown-cpu'")

  ' 2. At with initializer
  WriteTextFile(srcPath, "Public Static x As Integer At 0x1000 = 0\nPublic Function main() As Integer\n  Return 0\nEnd Function\n")
  snprintf(cmd, 1536, "'%s' '%s' --emit-only", comp, srcPath)
  Assert RunShell(cmd) <> 0
  Assert OutHas("Fixed-address 'At' Static variable cannot have an initializer")

  ' 3. At with non-literal
  WriteTextFile(srcPath, "Public Static BASE As Integer = 100\nPublic Static x As Integer At BASE\nPublic Function main() As Integer\n  Return 0\nEnd Function\n")
  snprintf(cmd, 1536, "'%s' '%s' --emit-only", comp, srcPath)
  Assert RunShell(cmd) <> 0
  Assert OutHas("'At' fixed memory address must be an integer literal")

  ' 4. At on local Dim
  WriteTextFile(srcPath, "Public Function main() As Integer\n  Dim reg As Integer At 0x40020000\n  Return 0\nEnd Function\n")
  snprintf(cmd, 1536, "'%s' '%s' --emit-only", comp, srcPath)
  Assert RunShell(cmd) <> 0
  Assert OutHas("'At' fixed memory address is only allowed on module-level Static declarations")

  ' 5. Invalid VolatileLoad address (Double instead of Pointer/Integer)
  WriteTextFile(srcPath, "Public Function main() As Integer\n  Dim d As Double = 3.14\n  Dim x As Integer = VolatileLoad(d, Integer)\n  Return 0\nEnd Function\n")
  snprintf(cmd, 1536, "'%s' '%s' --emit-only", comp, srcPath)
  Assert RunShell(cmd) <> 0
  Assert OutHas("VolatileLoad/VolatileStore address must be a Pointer or integer")

  ' 6. Invalid VolatileStore type mismatch
  WriteTextFile(srcPath, "Public Function main() As Integer\n  Dim p As Pointer = 0\n  VolatileStore(p, Integer, \"bad\")\n  Return 0\nEnd Function\n")
  snprintf(cmd, 1536, "'%s' '%s' --emit-only", comp, srcPath)
  Assert RunShell(cmd) <> 0
  Assert OutHas("VolatileStore value type mismatch")

  snprintf(mkCmd, 512, "rm -rf '%s'", tmpDir)
  system(mkCmd)
End Test

Test "std integration: combined smoke test of all 4 std modules with zero symbol collisions"
  Dim comp(768), tmpDir(256), srcPath(300), binPath(300) As Byte
  GetCompilerAbs(comp, 768)
  Dim pid As Integer = getpid()
  snprintf(tmpDir, 256, "tests/tmp_vb_tgt_%d_std", pid)
  snprintf(srcPath, 300, "%s/smoke.vb", tmpDir)
  snprintf(binPath, 300, "%s/smoke", tmpDir)

  Dim mkCmd(512) As Byte
  snprintf(mkCmd, 512, "mkdir -p '%s'", tmpDir)
  system(mkCmd)

  ' Include "std/..." resolves via compiler include search path (cwd fallback)
  WriteTextFile(srcPath, "Include \"std/strings.vb\"\nInclude \"std/math.vb\"\nInclude \"std/collections.vb\"\nInclude \"std/io.vb\"\n\nPublic Function main() As Integer\n  Dim a As Arena = ArenaCreate(4096)\n\n  ' 1. std/strings.vb: StrViewFrom & StrLen\n  Dim sv As StrView = StrViewFrom(\"verbose-cross\")\n  Dim sLen As Integer = StrLen(\"verbose-cross\")\n  If sv.length <> sLen Then\n    Return 1\n  End If\n\n  ' 2. std/math.vb: MathAbsI & MathClampI\n  Dim absVal As Integer = MathAbsI(-123)\n  Dim cl As Integer = MathClampI(absVal, 0, 100)\n  If cl <> 100 Then\n    Return 2\n  End If\n\n  ' 3. std/collections.vb: IntVec\n  Dim iv As IntVec = IntVecNew(ByRef a, 8)\n  IntVecPush(ByRef iv, absVal)\n  IntVecPush(ByRef iv, cl)\n  If iv.length <> 2 Then\n    Return 3\n  End If\n  If IntVecGet(ByRef iv, 0) <> 123 Then\n    Return 4\n  End If\n\n  ' 4. std/io.vb: IoFileSize\n  Dim fsz As Long = IoFileSize(\"src/ast.vb\")\n  If fsz <= 0 Then\n    Return 5\n  End If\n\n  ArenaDestroy(ByRef a)\n  Printf(\"std smoke test passed: sv=%d abs=%d vec=%d fsz=%ld\\n\", sv.length, absVal, iv.length, fsz)\n  Return 0\nEnd Function\n")

  Dim cmd(1536) As Byte
  snprintf(cmd, 1536, "'%s' '%s' -o '%s'", comp, srcPath, binPath)
  Assert RunShell(cmd) = 0
  Assert FileExists(binPath)

  snprintf(cmd, 1536, "'%s'", binPath)
  Assert RunShell(cmd) = 0
  Assert OutHas("std smoke test passed: sv=13 abs=123 vec=2")

  snprintf(mkCmd, 512, "rm -rf '%s'", tmpDir)
  system(mkCmd)
End Test
