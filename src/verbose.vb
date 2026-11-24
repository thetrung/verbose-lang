' VerBose Self-Hosting Compiler (verbose) - Full CLI Driver & Built-in Test Runner
' Compiles VerBose-Lang programs (.vb) -> Semantic Type Check -> LLVM IR -> Native Binary -> Execution.
' Written in VerBose-Lang and capable of compiling itself (Stage 1 -> Stage 2 -> Stage 3 fixpoint).

Include "printer.vb"
Include "type_analyzer.vb"
Include "codegen.vb"

Declare Function system(cmd As Pointer) As Integer
Declare Function strcmp(a As Pointer, b As Pointer) As Integer
Declare Function strncmp(a As Pointer, b As Pointer, n As Long) As Integer
Declare Function strlen(s As Pointer) As Long
Declare Function getpid() As Integer
Declare Function popen(cmd As Pointer, mode As Pointer) As Pointer
Declare Function pclose(fp As Pointer) As Integer
Declare Function unlink(path As Pointer) As Integer
Declare Function fflush(fp As Pointer) As Integer
Declare Function snprintf(buf As Pointer, sz As Long, fmt As Pointer, ...) As Integer

Private Sub PrintUsage()
  Printf("Usage: verbose <filename>.vb [options]\n       verbose test [file.vb | dir] [options]\n  test         Run built-in Test/Assure blocks in a .vb file or directory (default: test)\n  --test       Alias for 'verbose test'\n  --ast        Show parsed AST\n  --ir         Show generated LLVM-IR\n  --ta         Verbose type-analyzer pass (runs automatically before codegen)\n  --no-ta      Skip semantic type-analyzer pass\n  --emit-only  Emit .ll only (skip clang & execution)\n  --no-run     Compile binary with clang without executing it\n  -I <dir>     Add module Include search directory\n  -o <path>    Specify output binary path (or .ll path with --emit-only)\n  -l<lib>      Link external C library (e.g. -lm, -lraylib)\n  -L<dir>      Add library search path for clang\n  <file.c/.o>  Additional C/object files to compile and link with clang\n  -h, --help   Show this help message\n  --version    Show compiler version\n")
End Sub

Private Function CliHasSuffix(s As Pointer, suf As Pointer) As Boolean
  Dim slen As Long = strlen(s)
  Dim flen As Long = strlen(suf)
  If slen < flen Then
    Return False
  End If
  Dim start As Integer = slen - flen
  Dim i As Integer = 0
  Dim fInt As Integer = flen
  While i < fInt Do
    If s(start + i) <> suf(i) Then
      Return False
    End If
    i = i + 1
  End While
  Return True
End Function

Private Function CliHasSlash(s As Pointer) As Boolean
  Dim slen As Integer = strlen(s)
  Dim i As Integer = 0
  While i < slen Do
    If s(i) = 47 Then
      Return True
    End If
    i = i + 1
  End While
  Return False
End Function

Private Sub CliAppendChar(buf As Pointer, ByRef pos As Integer, cap As Integer, ch As Integer)
  If pos < (cap - 1) Then
    buf(pos) = ch
    pos = pos + 1
    buf(pos) = 0
  End If
End Sub

Private Sub CliAppendStr(buf As Pointer, ByRef pos As Integer, cap As Integer, s As Pointer)
  Dim slen As Integer = strlen(s)
  Dim i As Integer = 0
  While i < slen Do
    CliAppendChar(buf, ByRef pos, cap, s(i))
    i = i + 1
  End While
End Sub

Private Sub CliAppendQuoted(buf As Pointer, ByRef pos As Integer, cap As Integer, s As Pointer)
  CliAppendChar(buf, ByRef pos, cap, 39)
  Dim slen As Integer = strlen(s)
  Dim i As Integer = 0
  While i < slen Do
    Dim ch As Integer = s(i)
    If ch = 39 Then
      CliAppendStr(buf, ByRef pos, cap, "'\\''")
    Else
      CliAppendChar(buf, ByRef pos, cap, ch)
    End If
    i = i + 1
  End While
  CliAppendChar(buf, ByRef pos, cap, 39)
End Sub

Private Sub CliStripVbExt(src As Pointer, dst As Pointer, cap As Integer)
  Dim slen As Integer = strlen(src)
  Dim copyLen As Integer = slen
  If CliHasSuffix(src, ".vb") Then
    copyLen = slen - 3
  End If
  Dim pos As Integer = 0
  Dim i As Integer = 0
  While i < copyLen Do
    CliAppendChar(dst, ByRef pos, cap, src(i))
    i = i + 1
  End While
  dst(pos) = 0
End Sub

Private Sub CliPrintFile(path As Pointer)
  Dim fp As Pointer = Fopen(path, "rb")
  If IsNull(fp) Then
    Return
  End If
  Dim ch As Integer = Getc(fp)
  While ch >= 0 Do
    Printf("%c", ch)
    ch = Getc(fp)
  End While
  fclose(fp)
End Sub

Private Function CliIsDir(path As Pointer) As Boolean
  Dim cmd(1024) As Byte
  Dim pos As Integer = 0
  CliAppendStr(cmd, ByRef pos, 1024, "test -d ")
  CliAppendQuoted(cmd, ByRef pos, 1024, path)
  Return system(cmd) = 0
End Function

Private Function CliCountTests(progRoot As Integer) As Integer
  Dim defStart As Integer = g_nodes(progRoot).firstChild
  Dim defCount As Integer = g_nodes(progRoot).childCount
  Dim cnt As Integer = 0
  Dim i As Integer = 0
  While i < defCount Do
    Dim d As Integer = g_kids(defStart + i)
    If g_nodes(d).kind = NodeKind.NdTestDef Then
      cnt = cnt + 1
    End If
    i = i + 1
  End While
  Return cnt
End Function

Private Function RunSingleTarget(inPath As Pointer, testMode As Integer, showAst As Integer, showIr As Integer, showTa As Integer, skipTa As Integer, emitOnly As Integer, noRun As Integer, outPath As Pointer, extraInputs() As Pointer, extraInputCount As Integer, clangFlags() As Pointer, clangFlagCount As Integer) As Integer
  ' 32 MiB deterministic Arena for all source pools, tokens, AST nodes, and symbol tables
  Dim arena As Arena = ArenaCreate(33554432)
  Dim tokCount As Integer = LexerLoadEntry(ByRef arena, inPath)
  If tokCount < 0 Then
    ArenaDestroy(ByRef arena)
    Return 1
  End If

  AstInit(ByRef arena)
  Dim progRoot As Integer = ParseProgram()
  If g_nodes(progRoot).op > 0 Then
    Printf("verbose: aborting due to %d parse error(s)\n", g_nodes(progRoot).op)
    ArenaDestroy(ByRef arena)
    Return 1
  End If

  If showAst = 1 Then
    Printf("\n --------- Parsed Source Code ---------\n")
    PrintProgramAst(progRoot)
  End If

  If skipTa = 0 Then
    Dim taErrs As Integer = AnalyzeProgram(ByRef arena, progRoot)
    If taErrs > 0 Then
      ArenaDestroy(ByRef arena)
      Return 1
    End If
    If showTa = 1 Then
      Printf(" [OK] Type Analyzer: semantic check passed.\n")
    End If
  End If

  If testMode = 1 Then
    Dim nTests As Integer = CliCountTests(progRoot)
    If nTests = 0 Then
      Printf("verbose test error: no Test blocks found in '%s'\n", inPath)
      ArenaDestroy(ByRef arena)
      Return 1
    End If
  End If

  CgSetTestMode(testMode)

  ' Determine .ll and binary output paths
  Dim baseBuf(512) As Byte
  CliStripVbExt(inPath, baseBuf, 512)

  Dim llPathBuf(512) As Byte
  Dim llPos As Integer = 0
  Dim cleanupLl As Integer = 0
  Dim cleanupBin As Integer = 0
  Dim pid As Integer = getpid()

  If (emitOnly = 1) And (Not IsNull(outPath)) Then
    CliAppendStr(llPathBuf, ByRef llPos, 512, outPath)
  Else
    If (testMode = 1) And (emitOnly = 0) Then
      snprintf(llPathBuf, 512, "/tmp/vb_test_%d.ll", pid)
      cleanupLl = 1
    Else
      CliAppendStr(llPathBuf, ByRef llPos, 512, baseBuf)
      CliAppendStr(llPathBuf, ByRef llPos, 512, ".ll")
    End If
  End If

  Dim rc As Integer = EmitProgramToFile(ByRef arena, progRoot, llPathBuf)
  ArenaDestroy(ByRef arena)
  If rc <> 0 Then
    If cleanupLl = 1 Then
      unlink(llPathBuf)
    End If
    Return 1
  End If

  If showIr = 1 Then
    Printf("\n ------- GENERATED LLVM IR CODE -------\n")
    CliPrintFile(llPathBuf)
  End If

  If emitOnly = 1 Then
    Return 0
  End If

  ' Determine output binary path
  Dim binPathBuf(512) As Byte
  Dim binPos As Integer = 0
  If Not IsNull(outPath) Then
    CliAppendStr(binPathBuf, ByRef binPos, 512, outPath)
  Else
    If testMode = 1 Then
      snprintf(binPathBuf, 512, "/tmp/vb_test_%d", pid)
      If noRun = 0 Then
        cleanupBin = 1
      End If
    Else
      CliAppendStr(binPathBuf, ByRef binPos, 512, baseBuf)
    End If
  End If

  ' Build clang -O2 command
  Dim cmdBuf(4096) As Byte
  Dim cmdPos As Integer = 0
  CliAppendStr(cmdBuf, ByRef cmdPos, 4096, "clang -O2 ")
  CliAppendQuoted(cmdBuf, ByRef cmdPos, 4096, llPathBuf)
  Dim ei As Integer = 0
  While ei < extraInputCount Do
    CliAppendChar(cmdBuf, ByRef cmdPos, 4096, 32)
    CliAppendQuoted(cmdBuf, ByRef cmdPos, 4096, extraInputs(ei))
    ei = ei + 1
  End While
  CliAppendStr(cmdBuf, ByRef cmdPos, 4096, " -o ")
  CliAppendQuoted(cmdBuf, ByRef cmdPos, 4096, binPathBuf)
  Dim fi As Integer = 0
  While fi < clangFlagCount Do
    CliAppendChar(cmdBuf, ByRef cmdPos, 4096, 32)
    CliAppendQuoted(cmdBuf, ByRef cmdPos, 4096, clangFlags(fi))
    fi = fi + 1
  End While

  Dim clangRc As Integer = system(cmdBuf)
  If cleanupLl = 1 Then
    unlink(llPathBuf)
  End If
  If clangRc <> 0 Then
    If cleanupBin = 1 Then
      unlink(binPathBuf)
    End If
    Printf("Status: clang = %d\n", clangRc)
    Return 1
  End If

  If noRun = 1 Then
    Return 0
  End If

  ' Execute compiled binary directly
  Dim runBuf(1024) As Byte
  Dim runPos As Integer = 0
  If Not CliHasSlash(binPathBuf) Then
    Dim relBuf(520) As Byte
    Dim relPos As Integer = 0
    CliAppendStr(relBuf, ByRef relPos, 520, "./")
    CliAppendStr(relBuf, ByRef relPos, 520, binPathBuf)
    CliAppendQuoted(runBuf, ByRef runPos, 1024, relBuf)
  Else
    CliAppendQuoted(runBuf, ByRef runPos, 1024, binPathBuf)
  End If

  Dim execRc As Integer = system(runBuf)
  If cleanupBin = 1 Then
    unlink(binPathBuf)
  End If
  If execRc <> 0 Then
    Dim exitCode As Integer = (execRc Shr 8) And 255
    If exitCode = 0 Then
      Return 1
    End If
    Return exitCode
  End If
  Return 0
End Function

Private Function RunTestDirectory(dirPath As Pointer, showAst As Integer, showIr As Integer, showTa As Integer, skipTa As Integer, emitOnly As Integer, noRun As Integer, extraInputs() As Pointer, extraInputCount As Integer, clangFlags() As Pointer, clangFlagCount As Integer) As Integer
  ' Trim trailing slashes unless path is "/"
  Dim cleanDir(512) As Byte
  Dim dlen As Integer = strlen(dirPath)
  While (dlen > 1) And (dirPath(dlen - 1) = 47) Do
    dlen = dlen - 1
  End While
  Dim di As Integer = 0
  While (di < dlen) And (di < 511) Do
    cleanDir(di) = dirPath(di)
    di = di + 1
  End While
  cleanDir(di) = 0

  Dim findCmd(1024) As Byte
  Dim fPos As Integer = 0
  CliAppendStr(findCmd, ByRef fPos, 1024, "find ")
  CliAppendQuoted(findCmd, ByRef fPos, 1024, cleanDir)
  CliAppendStr(findCmd, ByRef fPos, 1024, " -maxdepth 1 -type f \\( -name 'test_*.vb' -o -name '*_test.vb' \\) | sort")

  Dim fp As Pointer = popen(findCmd, "r")
  If IsNull(fp) Then
    Printf("verbose test error: failed to scan directory '%s'\n", cleanDir)
    Return 1
  End If

  Dim listBuf(32768) As Byte
  Dim listLen As Integer = 0
  Dim ch As Integer = Getc(fp)
  While (ch >= 0) And (listLen < 32766) Do
    listBuf(listLen) = ch
    listLen = listLen + 1
    ch = Getc(fp)
  End While
  listBuf(listLen) = 0
  pclose(fp)

  Dim suitesPassed As Integer = 0
  Dim suitesFailed As Integer = 0
  Dim nullOut As Pointer = 0
  Dim pos As Integer = 0

  While pos < listLen Do
    Dim curFile(512) As Byte
    Dim cLen As Integer = 0
    While (pos < listLen) And (listBuf(pos) <> 10) Do
      If (listBuf(pos) <> 13) And (cLen < 511) Then
        curFile(cLen) = listBuf(pos)
        cLen = cLen + 1
      End If
      pos = pos + 1
    End While
    If (pos < listLen) And (listBuf(pos) = 10) Then
      pos = pos + 1
    End If
    curFile(cLen) = 0
    If cLen > 0 Then
      Printf("\n=== Running %s ===\n", curFile)
      fflush(0)
      Dim rc As Integer = RunSingleTarget(curFile, 1, showAst, showIr, showTa, skipTa, emitOnly, noRun, nullOut, extraInputs, extraInputCount, clangFlags, clangFlagCount)
      If rc = 0 Then
        suitesPassed = suitesPassed + 1
      Else
        suitesFailed = suitesFailed + 1
      End If
    End If
  End While

  Dim totalSuites As Integer = suitesPassed + suitesFailed
  If totalSuites = 0 Then
    Printf("verbose test error: no test_*.vb or *_test.vb files found in '%s'\n", cleanDir)
    Return 1
  End If

  Printf("\n========================================\n")
  Printf("Test suites: %d/%d passed, %d failed.\n", suitesPassed, totalSuites, suitesFailed)
  fflush(0)
  If suitesFailed > 0 Then
    Return 1
  End If
  Return 0
End Function

Public Function main(argc As Integer, argv() As Pointer) As Integer
  If argc < 2 Then
    PrintUsage()
    Return 0
  End If

  Dim testMode As Integer = 0
  Dim showAst As Integer = 0
  Dim showIr As Integer = 0
  Dim showTa As Integer = 0
  Dim skipTa As Integer = 0
  Dim emitOnly As Integer = 0
  Dim noRun As Integer = 0
  Dim targets(64) As Pointer
  Dim targetCount As Integer = 0
  Dim outPath As Pointer = 0
  Dim extraInputs(32) As Pointer
  Dim extraInputCount As Integer = 0
  Dim clangFlags(32) As Pointer
  Dim clangFlagCount As Integer = 0

  LexerClearIncludeDirs()

  Dim i As Integer = 1
  If strcmp(argv(1), "test") = 0 Then
    testMode = 1
    i = 2
  End If

  While i < argc Do
    Dim arg As Pointer = argv(i)
    Dim handled As Integer = 0

    If (strcmp(arg, "-h") = 0) Or (strcmp(arg, "--help") = 0) Then
      PrintUsage()
      Return 0
    End If
    If strcmp(arg, "--version") = 0 Then
      Printf("verbose 0.3.0 (VerBose-Lang Self-Hosted Compiler)\n")
      Return 0
    End If
    If strcmp(arg, "--test") = 0 Then
      testMode = 1
      handled = 1
    End If
    If (handled = 0) And (strcmp(arg, "--ast") = 0) Then
      showAst = 1
      handled = 1
    End If
    If (handled = 0) And (strcmp(arg, "--ir") = 0) Then
      showIr = 1
      handled = 1
    End If
    If (handled = 0) And (strcmp(arg, "--ta") = 0) Then
      showTa = 1
      handled = 1
    End If
    If (handled = 0) And (strcmp(arg, "--no-ta") = 0) Then
      skipTa = 1
      handled = 1
    End If
    If (handled = 0) And (strcmp(arg, "--emit-only") = 0) Then
      emitOnly = 1
      handled = 1
    End If
    If (handled = 0) And (strcmp(arg, "--no-run") = 0) Then
      noRun = 1
      handled = 1
    End If
    If (handled = 0) And (strcmp(arg, "-o") = 0) Then
      If (i + 1) >= argc Then
        Printf("Error: -o requires a path argument\n")
        PrintUsage()
        Return 1
      End If
      i = i + 1
      outPath = argv(i)
      handled = 1
    End If
    If (handled = 0) And (strcmp(arg, "-I") = 0) Then
      If (i + 1) >= argc Then
        Printf("Error: -I requires a directory argument\n")
        PrintUsage()
        Return 1
      End If
      i = i + 1
      LexerAddIncludeDir(argv(i))
      handled = 1
    End If
    If (handled = 0) And (strncmp(arg, "-I", 2) = 0) And (strlen(arg) > 2) Then
      Static incInlineBuf(4096) As Byte
      Static incInlineOff As Integer = 0
      Dim startOff As Integer = incInlineOff
      Dim alen As Integer = strlen(arg)
      Dim k As Integer = 2
      While (k < alen) And (incInlineOff < 4094) Do
        incInlineBuf(incInlineOff) = arg(k)
        incInlineOff = incInlineOff + 1
        k = k + 1
      End While
      incInlineBuf(incInlineOff) = 0
      incInlineOff = incInlineOff + 1
      Dim dirPtr As Pointer = Malloc(alen)
      Dim m As Integer = 0
      While (startOff + m) < incInlineOff Do
        dirPtr(m) = incInlineBuf(startOff + m)
        m = m + 1
      End While
      LexerAddIncludeDir(dirPtr)
      handled = 1
    End If
    If (handled = 0) And ((strncmp(arg, "-l", 2) = 0) Or (strncmp(arg, "-L", 2) = 0) Or (strncmp(arg, "-W", 2) = 0)) Then
      If clangFlagCount < 32 Then
        clangFlags(clangFlagCount) = arg
        clangFlagCount = clangFlagCount + 1
      End If
      handled = 1
    End If
    If (handled = 0) And (arg(0) = 45) Then
      Printf("Unknown option: %s\n", arg)
      PrintUsage()
      Return 1
    End If
    If handled = 0 Then
      If CliHasSuffix(arg, ".c") Or CliHasSuffix(arg, ".o") Or CliHasSuffix(arg, ".a") Then
        If extraInputCount < 32 Then
          extraInputs(extraInputCount) = arg
          extraInputCount = extraInputCount + 1
        End If
        handled = 1
      Else
        If (targetCount > 0) And (testMode = 0) And CliHasSuffix(arg, ".ll") And IsNull(outPath) Then
          emitOnly = 1
          outPath = arg
          handled = 1
        Else
          If (targetCount = 0) Or (testMode = 1) Then
            If targetCount < 64 Then
              targets(targetCount) = arg
              targetCount = targetCount + 1
            End If
            handled = 1
          Else
            Printf("Unexpected argument: %s\n", arg)
            PrintUsage()
            Return 1
          End If
        End If
      End If
    End If
    i = i + 1
  End While

  If testMode = 1 Then
    If targetCount = 0 Then
      Return RunTestDirectory("test", showAst, showIr, showTa, skipTa, emitOnly, noRun, extraInputs, extraInputCount, clangFlags, clangFlagCount)
    End If
    If targetCount = 1 Then
      If CliIsDir(targets(0)) Then
        Return RunTestDirectory(targets(0), showAst, showIr, showTa, skipTa, emitOnly, noRun, extraInputs, extraInputCount, clangFlags, clangFlagCount)
      End If
      Return RunSingleTarget(targets(0), 1, showAst, showIr, showTa, skipTa, emitOnly, noRun, outPath, extraInputs, extraInputCount, clangFlags, clangFlagCount)
    End If
    Dim suitesPassed As Integer = 0
    Dim suitesFailed As Integer = 0
    Dim ti As Integer = 0
    While ti < targetCount Do
      Printf("\n=== Running %s ===\n", targets(ti))
      fflush(0)
      Dim trc As Integer = 0
      If CliIsDir(targets(ti)) Then
        trc = RunTestDirectory(targets(ti), showAst, showIr, showTa, skipTa, emitOnly, noRun, extraInputs, extraInputCount, clangFlags, clangFlagCount)
      Else
        trc = RunSingleTarget(targets(ti), 1, showAst, showIr, showTa, skipTa, emitOnly, noRun, outPath, extraInputs, extraInputCount, clangFlags, clangFlagCount)
      End If
      If trc = 0 Then
        suitesPassed = suitesPassed + 1
      Else
        suitesFailed = suitesFailed + 1
      End If
      ti = ti + 1
    End While
    Printf("\n========================================\n")
    Printf("Test suites: %d/%d passed, %d failed.\n", suitesPassed, targetCount, suitesFailed)
    If suitesFailed > 0 Then
      Return 1
    End If
    Return 0
  End If

  If targetCount = 0 Then
    PrintUsage()
    Return 1
  End If

  Return RunSingleTarget(targets(0), 0, showAst, showIr, showTa, skipTa, emitOnly, noRun, outPath, extraInputs, extraInputCount, clangFlags, clangFlagCount)
End Function
