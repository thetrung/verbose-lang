' CLI flag checks & root prototype.vb smoke test.

Include "harness.vb"

Test "cli: verbose hello.vb (compile + direct execute)"
  Dim comp(768) As Byte
  GetCompilerAbs(comp, 768)
  Dim pid As Integer = getpid()
  Dim tmpDir(256) As Byte
  Dim samplePath(300) As Byte
  Dim binPath(300) As Byte
  Dim llPath(300) As Byte
  snprintf(tmpDir, 256, "/tmp/vb_cli_%d", pid)
  snprintf(samplePath, 300, "%s/hello.vb", tmpDir)
  snprintf(binPath, 300, "%s/hello", tmpDir)
  snprintf(llPath, 300, "%s/hello.ll", tmpDir)

  Dim mkCmd(512) As Byte
  snprintf(mkCmd, 512, "mkdir -p '%s'", tmpDir)
  system(mkCmd)
  WriteTextFile(samplePath, "Structure Box\n  Dim v As Integer\nEnd Structure\nPublic Function main() As Integer\n  Dim b As Box With ( v = 42 )\n  Printf(\"hello=%d\\n\", b.v)\n  Return 0\nEnd Function\n")

  Dim cmd(1536) As Byte
  snprintf(cmd, 1536, "cd '%s' && '%s' hello.vb", tmpDir, comp)
  Assure RunShell(cmd) = 0
  Assure OutHas("hello=42")
  Assure FileExists(binPath)
  Assure FileExists(llPath)

  snprintf(mkCmd, 512, "rm -rf '%s'", tmpDir)
  system(mkCmd)
End Test

Test "cli: --ast, --ir, --no-run, --emit-only, --ta, bad-code rejection, --help, --version"
  Dim comp(768) As Byte
  GetCompilerAbs(comp, 768)
  Dim pid As Integer = getpid()
  Dim tmpDir(256) As Byte
  Dim samplePath(300) As Byte
  Dim badPath(300) As Byte
  Dim customBin(300) As Byte
  Dim customLl(300) As Byte
  snprintf(tmpDir, 256, "/tmp/vb_cli2_%d", pid)
  snprintf(samplePath, 300, "%s/hello.vb", tmpDir)
  snprintf(badPath, 300, "%s/bad.vb", tmpDir)
  snprintf(customBin, 300, "%s/custom_bin", tmpDir)
  snprintf(customLl, 300, "%s/custom.ll", tmpDir)

  Dim mkCmd(512) As Byte
  snprintf(mkCmd, 512, "mkdir -p '%s'", tmpDir)
  system(mkCmd)
  WriteTextFile(samplePath, "Structure Box\n  Dim v As Integer\nEnd Structure\nPublic Function main() As Integer\n  Dim b As Box With ( v = 42 )\n  Printf(\"hello=%d\\n\", b.v)\n  Return 0\nEnd Function\n")
  WriteTextFile(badPath, "Public Function main() As Integer\n  Return \"oops\"\nEnd Function\n")

  Dim cmd(1536) As Byte

  ' --ast --emit-only
  snprintf(cmd, 1536, "cd '%s' && '%s' hello.vb --ast --emit-only", tmpDir, comp)
  Assure RunShell(cmd) = 0
  Assure OutHas("Structure Box")
  Assure Not OutHas("hello=42")

  ' --ir --no-run -o custom_bin
  snprintf(cmd, 1536, "cd '%s' && '%s' hello.vb --ir --no-run -o custom_bin", tmpDir, comp)
  Assure RunShell(cmd) = 0
  Assure OutHas("define i32 @main()")
  Assure FileExists(customBin)
  Assure Not OutHas("hello=42")

  ' --emit-only -o custom.ll
  snprintf(cmd, 1536, "cd '%s' && '%s' hello.vb --emit-only -o custom.ll", tmpDir, comp)
  Assure RunShell(cmd) = 0
  Assure FileExists(customLl)
  Assure FileHas(customLl, "define i32 @main()")

  ' --ta verbose pass
  snprintf(cmd, 1536, "cd '%s' && '%s' hello.vb --ta --emit-only", tmpDir, comp)
  Assure RunShell(cmd) = 0
  Assure OutHas("[OK] Type Analyzer")

  ' default semantic type check rejects bad code
  snprintf(cmd, 1536, "cd '%s' && '%s' bad.vb --emit-only", tmpDir, comp)
  Assure RunShell(cmd) <> 0
  Assure OutHas("Semantic Type Error")

  ' --help and --version
  snprintf(cmd, 1536, "'%s' --help", comp)
  Assure RunShell(cmd) = 0
  Assure OutHas("--emit-only")
  Assure OutHas("verbose test")

  snprintf(cmd, 1536, "'%s' --version", comp)
  Assure RunShell(cmd) = 0
  Assure OutHas("verbose")

  snprintf(mkCmd, 512, "rm -rf '%s'", tmpDir)
  system(mkCmd)
End Test

Test "prototype.vb smoke test"
  Assure RunVb("prototype.vb", "") = 0
  Assure OutHas("vec1.z ^ 2 =")
End Test
