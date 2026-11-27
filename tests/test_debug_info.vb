' VerBose Test Suite: Milestone 5 — DWARF Debug Info (-g / --debug)

Include "harness.vb"

Test "Emit LLVM DWARF metadata (-g / --debug) across entry and included modules"
  Dim comp(768), tmpDir(256), modPath(300), mainPath(300), llPath(300), binPath(300), objPath(300) As Byte
  GetCompilerAbs(comp, 768)
  Dim pid As Integer = getpid()
  snprintf(tmpDir, 256, "/tmp/vb_dbg_%d", pid)
  snprintf(modPath, 300, "%s/helper_mod.vb", tmpDir)
  snprintf(mainPath, 300, "%s/app_main.vb", tmpDir)
  snprintf(llPath, 300, "%s/app_main.ll", tmpDir)
  snprintf(binPath, 300, "%s/app_main", tmpDir)
  snprintf(objPath, 300, "%s/app_arm.o", tmpDir)
  Dim cmd(1536) As Byte
  snprintf(cmd, 1536, "mkdir -p '%s'", tmpDir)
  system(cmd)

  WriteTextFile(modPath, "Public Function HelperAdd(a, b As Integer) As Integer\n  Dim s As Integer = a + b\n  Return s * 2\nEnd Function\n")
  WriteTextFile(mainPath, "Include \"helper_mod.vb\"\n\nPublic Function Compute(x As Integer) As Integer\n  Dim r As Integer = HelperAdd(x, 5)\n  Return r + 1\nEnd Function\n\nPublic Function main() As Integer\n  Dim v As Integer = Compute(10)\n  If v = 31 Then\n    Return 0\n  End If\n  Return 1\nEnd Function\n")

  ' 1. Verify LLVM IR contains !DICompileUnit, !DIFile for both .vb files, !DISubprogram, and !DILocation
  snprintf(cmd, 1536, "'%s' -g --emit-only '%s' -o '%s'", comp, mainPath, llPath)
  Assert RunShell(cmd) = 0, "-g --emit-only succeeds"
  Assert FileHas(llPath, "!llvm.dbg.cu = !{!0}"), "IR emits !llvm.dbg.cu"
  Assert FileHas(llPath, "!DICompileUnit("), "IR emits !DICompileUnit"
  Assert FileHas(llPath, "filename: \"app_main.vb\""), "IR emits !DIFile for entry file"
  Assert FileHas(llPath, "filename: \"helper_mod.vb\""), "IR emits !DIFile for included module"
  Assert FileHas(llPath, "!DISubprogram(name: \"Compute\""), "IR emits !DISubprogram for Compute"
  Assert FileHas(llPath, "!DISubprogram(name: \"HelperAdd\""), "IR emits !DISubprogram for HelperAdd"
  Assert FileHas(llPath, "!DILocation(line: "), "IR emits !DILocation records"
  Assert FileHas(llPath, ", !dbg !"), "IR attaches !dbg metadata to instructions"

  ' 2. Verify without -g there is zero debug metadata overhead
  snprintf(cmd, 1536, "'%s' --emit-only '%s' -o '%s'", comp, mainPath, llPath)
  Assert RunShell(cmd) = 0, "default --emit-only succeeds"
  Assert Not FileHas(llPath, "!DICompileUnit("), "default build emits zero DWARF metadata"

  ' 3. Verify --debug compiles and runs cleanly with clang -O0 -g and embeds real DWARF line tables
  snprintf(cmd, 1536, "'%s' --debug --no-run '%s' -o '%s'", comp, mainPath, binPath)
  Assert RunShell(cmd) = 0, "--debug compile succeeds"
  snprintf(cmd, 1536, "'%s'", binPath)
  Assert RunShell(cmd) = 0, "--debug compiled binary runs and returns 0"

  ' 4. Inspect DWARF line table in compiled binary via objdump / llvm-dwarfdump
  snprintf(cmd, 1536, "objdump --dwarf=decodedline '%s'", binPath)
  Assert RunShell(cmd) = 0, "objdump --dwarf=decodedline succeeds on -g binary"
  Assert OutHas("app_main.vb"), "DWARF line table contains app_main.vb"
  Assert OutHas("helper_mod.vb"), "DWARF line table contains helper_mod.vb"

  ' 5. Freestanding arm-m cross-compilation with -g -c succeeds
  snprintf(cmd, 1536, "'%s' -g --target arm-m -c '%s' -o '%s'", comp, mainPath, objPath)
  Assert RunShell(cmd) = 0, "-g --target arm-m -c cross-compile succeeds"

  snprintf(cmd, 1536, "rm -rf '%s'", tmpDir)
  system(cmd)
End Test
