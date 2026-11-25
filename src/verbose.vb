' VerBose Self-Hosting Compiler (verbose) - Full CLI Driver & Built-in Test Runner
' Compiles VerBose-Lang programs (.vb) -> Semantic Type Check -> LLVM IR -> Native Binary -> Execution.
' Written in VerBose-Lang and capable of compiling itself (Stage 1 -> Stage 2 -> Stage 3 fixpoint).

Include "constants.vb"
Include "printer.vb"
Include "type_analyzer.vb"
Include "codegen.vb"

Declare Function system(cmd As Pointer) As Integer
Declare Function strcmp(a, b As Pointer) As Integer
Declare Function strncmp(a, b As Pointer, n As Long) As Integer
Declare Function strlen(s As Pointer) As Long
Declare Function getpid() As Integer
Declare Function popen(cmd, mode As Pointer) As Pointer
Declare Function pclose(fp As Pointer) As Integer
Declare Function unlink(path As Pointer) As Integer
Declare Function fflush(fp As Pointer) As Integer
Declare Function snprintf(buf As Pointer, sz As Long, fmt As Pointer, ...) As Integer
Declare Function getenv(name As Pointer) As Pointer
Declare Function strstr(haystack, needle As Pointer) As Pointer

Private Static cli_hostTarget As Integer = -1
Private Static cli_maxStackOverride As Long = -1

Private Function CliParseNonNegLong(s As Pointer) As Long
  If IsNull(s) Or (s(0) = 0) Then
    Return -1
  End If
  Dim val As Long = 0
  Dim idx As Integer = 0
  While s(idx) <> 0 Do
    Dim ch As Integer = Integer(s(idx)) And 255
    If (ch < 48) Or (ch > 57) Then
      Return -1
    End If
    val = val * 10 + Long(ch - 48)
    idx = idx + 1
  End While
  Return val
End Function

Private Sub PrintUsage()
  Printf(MSG_CLI_USAGE)
End Sub

Private Function CliResolveTarget(name As Pointer) As Integer
  Match name With
    Case "linux-x86_64", "linux-x64", "x86_64-pc-linux-gnu", "x86_64-linux-gnu" Then Return TgtLinuxX64
    Case "linux-arm64", "linux-aarch64", "aarch64-unknown-linux-gnu", "aarch64-linux-gnu" Then Return TgtLinuxArm64
    Case "macos-arm64", "darwin-arm64", "arm64-apple-darwin", "arm64-apple-macosx", "arm64-apple-macosx11.0.0" Then Return TgtMacosArm64
    Case "windows-x86_64", "windows-x64", "win64", "x86_64-w64-windows-gnu", "x86_64-pc-windows-msvc" Then Return TgtWindowsX64
    Case "arm-m", "cortex-m", "cortex-m4", "thumbv7em-none-eabi", "thumbv7em-unknown-none-eabi" Then Return TgtArmM
    Case _ Then Return -1
  End Match
End Function

Private Function CliTargetTriple(targetId As Integer) As Pointer
  Match targetId With
    Case TgtLinuxArm64 Then Return CLANG_TRIPLE_LINUX_ARM64
    Case TgtMacosArm64 Then Return CLANG_TRIPLE_MACOS_ARM64
    Case TgtWindowsX64 Then Return CLANG_TRIPLE_WINDOWS_X64
    Case TgtArmM Then Return CLANG_TRIPLE_ARM_M
    Case _ Then Return CLANG_TRIPLE_LINUX_X64
  End Match
End Function

Private Function CliDetectHostTarget() As Integer
  If cli_hostTarget >= 0 Then
    Return cli_hostTarget
  End If
  Dim envT As Pointer = getenv("VERBOSE_TARGET")
  If Not IsNull(envT) Then
    Dim et As Integer = CliResolveTarget(envT)
    If et >= 0 Then
      cli_hostTarget = et
      Return cli_hostTarget
    End If
  End If
  Dim envOs As Pointer = getenv("OS")
  If Not IsNull(envOs) Then
    If strcmp(envOs, "Windows_NT") = 0 Then
      cli_hostTarget = TgtWindowsX64
      Return cli_hostTarget
    End If
  End If
  Dim fp As Pointer = popen("uname -sm 2>/dev/null", "r")
  If Not IsNull(fp) Then
    Dim ubuf(128) As Byte
    Dim ulen As Integer = 0
    Dim ch As Integer = Getc(fp)
    While (ch >= 0) And (ulen < 126) Do
      ubuf(ulen) = ch
      ulen = ulen + 1
      ch = Getc(fp)
    End While
    ubuf(ulen) = 0
    pclose(fp)
    If Not IsNull(strstr(ubuf, "Darwin")) Then
      cli_hostTarget = TgtMacosArm64
      Return cli_hostTarget
    End If
    If (Not IsNull(strstr(ubuf, "MINGW"))) Or (Not IsNull(strstr(ubuf, "MSYS"))) Or (Not IsNull(strstr(ubuf, "CYGWIN"))) Then
      cli_hostTarget = TgtWindowsX64
      Return cli_hostTarget
    End If
    If (Not IsNull(strstr(ubuf, "aarch64"))) Or (Not IsNull(strstr(ubuf, "arm64"))) Then
      cli_hostTarget = TgtLinuxArm64
      Return cli_hostTarget
    End If
  End If
  cli_hostTarget = TgtLinuxX64
  Return cli_hostTarget
End Function

Private Function CliTempDir() As Pointer
  If CliDetectHostTarget() = TgtWindowsX64 Then
    Dim t1 As Pointer = getenv("TEMP")
    If (Not IsNull(t1)) And (t1(0) <> 0) Then
      Return t1
    End If
    Dim t2 As Pointer = getenv("TMP")
    If (Not IsNull(t2)) And (t2(0) <> 0) Then
      Return t2
    End If
    Return "."
  End If
  Return "/tmp"
End Function

Private Function CliHasSuffix(s, suf As Pointer) As Boolean
  Dim slen = strlen(s), flen = strlen(suf) As Long
  If slen < flen Then
    Return False
  End If
  Dim start = slen - flen, i = 0, fInt = flen As Integer
  While i < fInt Do
    If s(start + i) <> suf(i) Then
      Return False
    End If
    i = i + 1
  End While
  Return True
End Function

Private Function CliHasSlash(s As Pointer) As Boolean
  Dim slen = strlen(s), i = 0 As Integer
  While i < slen Do
    If (s(i) = 47) Or (s(i) = 92) Then
      Return True
    End If
    i = i + 1
  End While
  Return False
End Function

Private Sub CliAppendChar(buf As Pointer, ByRef pos As Integer, cap, ch As Integer)
  If pos < (cap - 1) Then
    buf(pos) = ch
    pos = pos + 1
    buf(pos) = 0
  End If
End Sub

Private Sub CliAppendStr(buf As Pointer, ByRef pos As Integer, cap As Integer, s As Pointer)
  Dim slen = strlen(s), i = 0 As Integer
  While i < slen Do
    CliAppendChar(buf, ByRef pos, cap, s(i))
    i = i + 1
  End While
End Sub

Private Sub CliAppendQuoted(buf As Pointer, ByRef pos As Integer, cap As Integer, s As Pointer)
  If CliDetectHostTarget() = TgtWindowsX64 Then
    CliAppendChar(buf, ByRef pos, cap, 34)
    Dim wlen = strlen(s), wi = 0 As Integer
    While wi < wlen Do
      Dim wch As Integer = s(wi)
      If wch = 34 Then
        CliAppendStr(buf, ByRef pos, cap, "\\\"")
      Else
        CliAppendChar(buf, ByRef pos, cap, wch)
      End If
      wi = wi + 1
    End While
    CliAppendChar(buf, ByRef pos, cap, 34)
    Return
  End If
  CliAppendChar(buf, ByRef pos, cap, 39)
  Dim slen = strlen(s), i = 0 As Integer
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

Private Sub CliStripVbExt(src, dst As Pointer, cap As Integer)
  Dim slen = strlen(src), copyLen = slen As Integer
  If CliHasSuffix(src, ".vb") Then
    copyLen = slen - 3
  End If
  Dim pos = 0, i = 0 As Integer
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
  If CliDetectHostTarget() = TgtWindowsX64 Then
    snprintf(cmd, 1024, "if exist \"%s\\*\" (exit /b 0) else (exit /b 1)", path)
    Return system(cmd) = 0
  End If
  CliAppendStr(cmd, ByRef pos, 1024, "test -d ")
  CliAppendQuoted(cmd, ByRef pos, 1024, path)
  Return system(cmd) = 0
End Function

Private Function CliCountTests(progRoot As Integer) As Integer
  Dim defStart As Integer = g_nodes(progRoot).firstChild
  Dim defCount As Integer = g_nodes(progRoot).childCount
  Dim cnt As Integer = 0
  For i = 0 To defCount - 1 Do
    Dim d As Integer = g_kids(defStart + i)
    If g_nodes(d).kind = NdTestDef Then
      cnt = cnt + 1
    End If
  End For
  Return cnt
End Function

Private Function RunSingleTarget(inPath As Pointer, testMode, showAst, showIr, showTa, skipTa, emitOnly, emitObj, noRun, targetId As Integer, outPath As Pointer, extraInputs() As Pointer, extraInputCount As Integer, clangFlags() As Pointer, clangFlagCount As Integer) As Integer
  ' 64 MiB deterministic Arena for all source pools, tokens, AST nodes, and symbol tables
  Dim arena As Arena = ArenaCreate(67108864)
  Dim tokCount As Integer = LexerLoadEntry(ByRef arena, inPath)
  If tokCount < 0 Then
    ArenaDestroy(ByRef arena)
    Return 1
  End If

  AstInit(ByRef arena)
  Dim progRoot As Integer = ParseProgram()
  If g_nodes(progRoot).op > 0 Then
    Printf(MSG_CLI_PARSE_ABORT, g_nodes(progRoot).op)
    ArenaDestroy(ByRef arena)
    Return 1
  End If

  If showAst = 1 Then
    Printf(MSG_CLI_AST_HEADER)
    PrintProgramAst(progRoot)
  End If

  If skipTa = 0 Then
    TaSetTarget(targetId)
    If cli_maxStackOverride >= 0 Then
      TaSetMaxStack(cli_maxStackOverride)
    End If
    Dim taErrs As Integer = AnalyzeProgram(ByRef arena, progRoot)
    If taErrs > 0 Then
      ArenaDestroy(ByRef arena)
      Return 1
    End If
    If showTa = 1 Then
      Printf(MSG_CLI_TA_OK)
    End If
  End If

  If testMode = 1 Then
    Dim nTests As Integer = CliCountTests(progRoot)
    If nTests = 0 Then
      Printf(MSG_CLI_TEST_NO_BLOCKS, inPath)
      ArenaDestroy(ByRef arena)
      Return 1
    End If
  End If

  CgSetTestMode(testMode)
  CgSetTarget(targetId)

  ' Cross-target execution/linking rules
  Dim hostTgt As Integer = CliDetectHostTarget()
  Dim effEmitObj As Integer = emitObj
  Dim effNoRun As Integer = noRun
  If targetId = TgtArmM Then
    If emitOnly = 0 Then
      effEmitObj = 1
    End If
    effNoRun = 1
  Else
    If targetId <> hostTgt Then
      effNoRun = 1
    End If
  End If

  ' Determine .ll and binary output paths
  Dim baseBuf(512), llPathBuf(512) As Byte
  CliStripVbExt(inPath, baseBuf, 512)
  Dim llPos = 0, cleanupLl = 0, cleanupBin = 0, pid = getpid() As Integer

  If (emitOnly = 1) And (Not IsNull(outPath)) Then
    CliAppendStr(llPathBuf, ByRef llPos, 512, outPath)
  Else
    If (testMode = 1) And (emitOnly = 0) Then
      snprintf(llPathBuf, 512, "%s/vb_test_%d.ll", CliTempDir(), pid)
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
    Printf(MSG_CLI_IR_HEADER)
    CliPrintFile(llPathBuf)
  End If

  If emitOnly = 1 Then
    Return 0
  End If

  ' Determine output binary or object path
  Dim binPathBuf(512) As Byte
  Dim binPos As Integer = 0
  If Not IsNull(outPath) Then
    CliAppendStr(binPathBuf, ByRef binPos, 512, outPath)
  Else
    If effEmitObj = 1 Then
      CliAppendStr(binPathBuf, ByRef binPos, 512, baseBuf)
      CliAppendStr(binPathBuf, ByRef binPos, 512, ".o")
    Else
      If testMode = 1 Then
        If targetId = TgtWindowsX64 Then
          snprintf(binPathBuf, 512, "%s/vb_test_%d.exe", CliTempDir(), pid)
        Else
          snprintf(binPathBuf, 512, "%s/vb_test_%d", CliTempDir(), pid)
        End If
        If effNoRun = 0 Then
          cleanupBin = 1
        End If
      Else
        CliAppendStr(binPathBuf, ByRef binPos, 512, baseBuf)
        If (targetId = TgtWindowsX64) And (Not CliHasSuffix(binPathBuf, ".exe")) Then
          CliAppendStr(binPathBuf, ByRef binPos, 512, ".exe")
        End If
      End If
    End If
  End If

  ' Build clang -O2 command
  Dim cmdBuf(4096) As Byte
  Dim cmdPos As Integer = 0
  CliAppendStr(cmdBuf, ByRef cmdPos, 4096, "clang -O2 ")
  If targetId <> hostTgt Then
    CliAppendStr(cmdBuf, ByRef cmdPos, 4096, "-target ")
    CliAppendStr(cmdBuf, ByRef cmdPos, 4096, CliTargetTriple(targetId))
    CliAppendChar(cmdBuf, ByRef cmdPos, 4096, 32)
  Else
    If targetId = TgtMacosArm64 Then
      CliAppendStr(cmdBuf, ByRef cmdPos, 4096, "-Wno-override-module ")
    End If
  End If
  If effEmitObj = 1 Then
    CliAppendStr(cmdBuf, ByRef cmdPos, 4096, "-c ")
  End If
  CliAppendQuoted(cmdBuf, ByRef cmdPos, 4096, llPathBuf)
  If effEmitObj = 0 Then
    For ei = 0 To extraInputCount - 1 Do
      CliAppendChar(cmdBuf, ByRef cmdPos, 4096, 32)
      CliAppendQuoted(cmdBuf, ByRef cmdPos, 4096, extraInputs(ei))
    End For
  End If
  CliAppendStr(cmdBuf, ByRef cmdPos, 4096, " -o ")
  CliAppendQuoted(cmdBuf, ByRef cmdPos, 4096, binPathBuf)
  For fi = 0 To clangFlagCount - 1 Do
    CliAppendChar(cmdBuf, ByRef cmdPos, 4096, 32)
    CliAppendQuoted(cmdBuf, ByRef cmdPos, 4096, clangFlags(fi))
  End For
  If (effEmitObj = 0) And ((targetId = TgtLinuxX64) Or (targetId = TgtLinuxArm64) Or (targetId = TgtWindowsX64)) And (CgUsesAsync() = 1) Then
    CliAppendStr(cmdBuf, ByRef cmdPos, 4096, " -lpthread")
  End If

  Dim clangRc As Integer = system(cmdBuf)
  If cleanupLl = 1 Then
    unlink(llPathBuf)
  End If
  If clangRc <> 0 Then
    If cleanupBin = 1 Then
      unlink(binPathBuf)
    End If
    Printf(MSG_CLI_CLANG_STATUS, clangRc)
    Return 1
  End If

  If (effEmitObj = 1) Or (effNoRun = 1) Then
    Return 0
  End If

  ' Execute compiled binary directly
  Dim runBuf(1024) As Byte
  Dim runPos As Integer = 0
  If Not CliHasSlash(binPathBuf) Then
    Dim relBuf(520) As Byte
    Dim relPos As Integer = 0
    If hostTgt = TgtWindowsX64 Then
      CliAppendStr(relBuf, ByRef relPos, 520, ".\\")
    Else
      CliAppendStr(relBuf, ByRef relPos, 520, "./")
    End If
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
    If hostTgt = TgtWindowsX64 Then
      Return execRc
    End If
    Dim exitCode As Integer = (execRc Shr 8) And 255
    If exitCode = 0 Then
      Return 1
    End If
    Return exitCode
  End If
  Return 0
End Function

Private Function RunTestDirectory(dirPath As Pointer, showAst, showIr, showTa, skipTa, emitOnly, emitObj, noRun, targetId As Integer, extraInputs() As Pointer, extraInputCount As Integer, clangFlags() As Pointer, clangFlagCount As Integer) As Integer
  ' Trim trailing slashes unless path is "/"
  Dim cleanDir(512) As Byte
  Dim dlen As Integer = strlen(dirPath)
  While (dlen > 1) And ((dirPath(dlen - 1) = 47) Or (dirPath(dlen - 1) = 92)) Do
    dlen = dlen - 1
  End While
  Dim di As Integer = 0
  While (di < dlen) And (di < 511) Do
    cleanDir(di) = dirPath(di)
    di = di + 1
  End While
  cleanDir(di) = 0

  Dim isWin As Boolean = (CliDetectHostTarget() = TgtWindowsX64)
  Dim findCmd(1024) As Byte
  Dim fPos As Integer = 0
  If isWin Then
    snprintf(findCmd, 1024, "dir /b /o:n \"%s\\test_*.vb\" \"%s\\*_test.vb\" 2>nul", cleanDir, cleanDir)
  Else
    CliAppendStr(findCmd, ByRef fPos, 1024, "find ")
    CliAppendQuoted(findCmd, ByRef fPos, 1024, cleanDir)
    CliAppendStr(findCmd, ByRef fPos, 1024, " -maxdepth 1 -type f \\( -name 'test_*.vb' -o -name '*_test.vb' \\) | sort")
  End If

  Dim fp As Pointer = popen(findCmd, "r")
  If IsNull(fp) Then
    Printf(MSG_CLI_TEST_SCAN_FAIL, cleanDir)
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
    Dim rawFile(512), curFile(512) As Byte
    Dim cLen As Integer = 0
    While (pos < listLen) And (listBuf(pos) <> 10) Do
      If (listBuf(pos) <> 13) And (cLen < 511) Then
        rawFile(cLen) = listBuf(pos)
        cLen = cLen + 1
      End If
      pos = pos + 1
    End While
    If (pos < listLen) And (listBuf(pos) = 10) Then
      pos = pos + 1
    End If
    rawFile(cLen) = 0
    If cLen > 0 Then
      If isWin And (Not CliHasSlash(rawFile)) Then
        snprintf(curFile, 512, "%s/%s", cleanDir, rawFile)
      Else
        snprintf(curFile, 512, "%s", rawFile)
      End If
      Printf(MSG_CLI_TEST_RUNNING, curFile)
      fflush(0)
      Dim rc As Integer = RunSingleTarget(curFile, 1, showAst, showIr, showTa, skipTa, emitOnly, emitObj, noRun, targetId, nullOut, extraInputs, extraInputCount, clangFlags, clangFlagCount)
      If rc = 0 Then
        suitesPassed = suitesPassed + 1
      Else
        suitesFailed = suitesFailed + 1
      End If
    End If
  End While

  Dim totalSuites As Integer = suitesPassed + suitesFailed
  If totalSuites = 0 Then
    Printf(MSG_CLI_TEST_NO_FILES, cleanDir)
    Return 1
  End If

  Printf(MSG_CLI_TEST_SEP)
  Printf(MSG_CLI_TEST_SUMMARY, suitesPassed, totalSuites, suitesFailed)
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

  Dim testMode, showAst, showIr, showTa, skipTa, emitOnly, emitObj, noRun As Integer = 0
  Dim targetId As Integer = CliDetectHostTarget()
  Dim targets(64), extraInputs(32), clangFlags(32) As Pointer
  Dim targetCount, extraInputCount, clangFlagCount As Integer = 0
  Dim outPath As Pointer = 0

  LexerClearIncludeDirs()

  Dim i As Integer = 1
  If strcmp(argv(1), "test") = 0 Then
    testMode = 1
    i = 2
  End If

  While i < argc Do
    Dim arg As Pointer = argv(i)
    Match arg With
      Case "-h", "--help" Then
        PrintUsage()
        Return 0
      Case "--version" Then
        Printf(MSG_CLI_VERSION)
        Return 0
      Case "--test" Then testMode = 1
      Case "--ast" Then showAst = 1
      Case "--ir" Then showIr = 1
      Case "--ta" Then showTa = 1
      Case "--no-ta" Then skipTa = 1
      Case "--max-stack" Then
        If (i + 1) >= argc Then
          Printf(MSG_CLI_ERR_STACK_ARG)
          PrintUsage()
          Return 1
        End If
        i = i + 1
        Dim parsedStack As Long = CliParseNonNegLong(argv(i))
        If parsedStack < 0 Then
          Printf(MSG_CLI_ERR_STACK_ARG)
          Return 1
        End If
        cli_maxStackOverride = parsedStack
      Case _ When strncmp(arg, "--max-stack=", 12) = 0 Then
        Dim parsedStack As Long = CliParseNonNegLong(PtrAdd(arg, 12))
        If parsedStack < 0 Then
          Printf(MSG_CLI_ERR_STACK_ARG)
          Return 1
        End If
        cli_maxStackOverride = parsedStack
      Case "--emit-only" Then emitOnly = 1
      Case "-c", "--emit-obj" Then emitObj = 1
      Case "--no-run" Then noRun = 1
      Case "--target" Then
        If (i + 1) >= argc Then
          Printf(MSG_CLI_ERR_TARGET_ARG)
          PrintUsage()
          Return 1
        End If
        i = i + 1
        Dim resolvedTgt As Integer = CliResolveTarget(argv(i))
        If resolvedTgt < 0 Then
          Printf(MSG_CLI_ERR_UNKNOWN_TARGET, argv(i))
          Return 1
        End If
        targetId = resolvedTgt
      Case _ When strncmp(arg, "--target=", 9) = 0 Then
        Dim tNameBuf(128) As Byte
        Dim alen As Integer = strlen(arg)
        Dim tk = 9, tp = 0 As Integer
        While (tk < alen) And (tp < 127) Do
          tNameBuf(tp) = arg(tk)
          tp = tp + 1
          tk = tk + 1
        End While
        tNameBuf(tp) = 0
        Dim resolvedTgt As Integer = CliResolveTarget(tNameBuf)
        If resolvedTgt < 0 Then
          Printf(MSG_CLI_ERR_UNKNOWN_TARGET, tNameBuf)
          Return 1
        End If
        targetId = resolvedTgt
      Case "-o" Then
        If (i + 1) >= argc Then
          Printf(MSG_CLI_ERR_O_ARG)
          PrintUsage()
          Return 1
        End If
        i = i + 1
        outPath = argv(i)
      Case "-I" Then
        If (i + 1) >= argc Then
          Printf(MSG_CLI_ERR_I_ARG)
          PrintUsage()
          Return 1
        End If
        i = i + 1
        LexerAddIncludeDir(argv(i))
      Case _ When (strncmp(arg, "-I", 2) = 0) And (strlen(arg) > 2) Then
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
      Case _ When (strncmp(arg, "-l", 2) = 0) Or (strncmp(arg, "-L", 2) = 0) Or (strncmp(arg, "-W", 2) = 0) Then
        If clangFlagCount < 32 Then
          clangFlags(clangFlagCount) = arg
          clangFlagCount = clangFlagCount + 1
        End If
      Case _ When arg(0) = 45 Then
        Printf(MSG_CLI_ERR_UNKNOWN_OPT, arg)
        PrintUsage()
        Return 1
      Case _ When CliHasSuffix(arg, ".c") Or CliHasSuffix(arg, ".o") Or CliHasSuffix(arg, ".a") Then
        If extraInputCount < 32 Then
          extraInputs(extraInputCount) = arg
          extraInputCount = extraInputCount + 1
        End If
      Case _ When (targetCount > 0) And (testMode = 0) And CliHasSuffix(arg, ".ll") And IsNull(outPath) Then
        emitOnly = 1
        outPath = arg
      Case _ When (targetCount = 0) Or (testMode = 1) Then
        If targetCount < 64 Then
          targets(targetCount) = arg
          targetCount = targetCount + 1
        End If
      Case _ Then
        Printf(MSG_CLI_ERR_UNEXPECTED_ARG, arg)
        PrintUsage()
        Return 1
    End Match
    i = i + 1
  End While

  If testMode = 1 Then
    If targetCount = 0 Then
      Return RunTestDirectory("tests", showAst, showIr, showTa, skipTa, emitOnly, emitObj, noRun, targetId, extraInputs, extraInputCount, clangFlags, clangFlagCount)
    End If
    If targetCount = 1 Then
      If CliIsDir(targets(0)) Then
        Return RunTestDirectory(targets(0), showAst, showIr, showTa, skipTa, emitOnly, emitObj, noRun, targetId, extraInputs, extraInputCount, clangFlags, clangFlagCount)
      End If
      Return RunSingleTarget(targets(0), 1, showAst, showIr, showTa, skipTa, emitOnly, emitObj, noRun, targetId, outPath, extraInputs, extraInputCount, clangFlags, clangFlagCount)
    End If
    Dim suitesPassed As Integer = 0
    Dim suitesFailed As Integer = 0
    For ti = 0 To targetCount - 1 Do
      Printf(MSG_CLI_TEST_RUNNING, targets(ti))
      fflush(0)
      Dim trc As Integer = 0
      If CliIsDir(targets(ti)) Then
        trc = RunTestDirectory(targets(ti), showAst, showIr, showTa, skipTa, emitOnly, emitObj, noRun, targetId, extraInputs, extraInputCount, clangFlags, clangFlagCount)
      Else
        trc = RunSingleTarget(targets(ti), 1, showAst, showIr, showTa, skipTa, emitOnly, emitObj, noRun, targetId, outPath, extraInputs, extraInputCount, clangFlags, clangFlagCount)
      End If
      If trc = 0 Then
        suitesPassed = suitesPassed + 1
      Else
        suitesFailed = suitesFailed + 1
      End If
    End For
    Printf(MSG_CLI_TEST_SEP)
    Printf(MSG_CLI_TEST_SUMMARY, suitesPassed, targetCount, suitesFailed)
    If suitesFailed > 0 Then
      Return 1
    End If
    Return 0
  End If

  If targetCount = 0 Then
    PrintUsage()
    Return 1
  End If

  Return RunSingleTarget(targets(0), 0, showAst, showIr, showTa, skipTa, emitOnly, emitObj, noRun, targetId, outPath, extraInputs, extraInputCount, clangFlags, clangFlagCount)
End Function
