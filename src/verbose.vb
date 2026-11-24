' VerBose Self-Hosting Compiler (verbose) - Full CLI Driver
' Compiles VerBose-Lang programs (.vb) -> Semantic Type Check -> LLVM IR -> Native Binary -> Execution.
' Written in VerBose-Lang and capable of compiling itself (Stage 1 -> Stage 2 -> Stage 3 fixpoint).

Include "printer.vb"
Include "type_analyzer.vb"
Include "codegen.vb"

Declare Function system(cmd As Pointer) As Integer
Declare Function strcmp(a As Pointer, b As Pointer) As Integer
Declare Function strncmp(a As Pointer, b As Pointer, n As Long) As Integer
Declare Function strlen(s As Pointer) As Long

Private Sub PrintUsage()
  Printf("Usage: verbose <filename>.vb [options]\n")
  Printf("  --ast        Show parsed AST\n")
  Printf("  --ir         Show generated LLVM-IR\n")
  Printf("  --ta         Verbose type-analyzer pass (runs automatically before codegen)\n")
  Printf("  --no-ta      Skip semantic type-analyzer pass\n")
  Printf("  --emit-only  Emit .ll only (skip clang & execution)\n")
  Printf("  --no-run     Compile binary with clang without executing it\n")
  Printf("  -I <dir>     Add module Include search directory\n")
  Printf("  -o <path>    Specify output binary path (or .ll path with --emit-only)\n")
  Printf("  -l<lib>      Link external C library (e.g. -lm, -lraylib)\n")
  Printf("  -L<dir>      Add library search path for clang\n")
  Printf("  <file.c/.o>  Additional C/object files to compile and link with clang\n")
  Printf("  -h, --help   Show this help message\n")
  Printf("  --version    Show compiler version\n")
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

Public Function main(argc As Integer, argv() As Pointer) As Integer
  If argc < 2 Then
    PrintUsage()
    Return 0
  End If

  Dim showAst As Integer = 0
  Dim showIr As Integer = 0
  Dim showTa As Integer = 0
  Dim skipTa As Integer = 0
  Dim emitOnly As Integer = 0
  Dim noRun As Integer = 0
  Dim inPath As Pointer = 0
  Dim outPath As Pointer = 0
  Dim extraInputs(32) As Pointer
  Dim extraInputCount As Integer = 0
  Dim clangFlags(32) As Pointer
  Dim clangFlagCount As Integer = 0

  LexerClearIncludeDirs()

  Dim i As Integer = 1
  While i < argc Do
    Dim arg As Pointer = argv(i)
    Dim handled As Integer = 0

    If (strcmp(arg, "-h") = 0) Or (strcmp(arg, "--help") = 0) Then
      PrintUsage()
      Return 0
    End If
    If strcmp(arg, "--version") = 0 Then
      Printf("verbose 0.2.0 (VerBose-Lang Self-Hosted Compiler)\n")
      Return 0
    End If
    If strcmp(arg, "--ast") = 0 Then
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
      ' Extract substring after "-I" into small heap/static copy? We can advance pointer via Arena later or store slice!
      ' Use a static buffer for -I<dir> inline flags
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
      If IsNull(inPath) Then
        inPath = arg
        handled = 1
      Else
        If CliHasSuffix(arg, ".c") Or CliHasSuffix(arg, ".o") Or CliHasSuffix(arg, ".a") Then
          If extraInputCount < 32 Then
            extraInputs(extraInputCount) = arg
            extraInputCount = extraInputCount + 1
          End If
          handled = 1
        Else
          If CliHasSuffix(arg, ".ll") And IsNull(outPath) Then
            emitOnly = 1
            outPath = arg
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

  If IsNull(inPath) Then
    PrintUsage()
    Return 1
  End If

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

  ' Determine .ll and binary output paths
  Dim baseBuf(512) As Byte
  CliStripVbExt(inPath, baseBuf, 512)

  Dim llPathBuf(512) As Byte
  Dim llPos As Integer = 0
  If (emitOnly = 1) And (Not IsNull(outPath)) Then
    CliAppendStr(llPathBuf, ByRef llPos, 512, outPath)
  Else
    CliAppendStr(llPathBuf, ByRef llPos, 512, baseBuf)
    CliAppendStr(llPathBuf, ByRef llPos, 512, ".ll")
  End If

  Dim rc As Integer = EmitProgramToFile(ByRef arena, progRoot, llPathBuf)
  ArenaDestroy(ByRef arena)
  If rc <> 0 Then
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
    CliAppendStr(binPathBuf, ByRef binPos, 512, baseBuf)
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
  If clangRc <> 0 Then
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
  If execRc <> 0 Then
    Dim exitCode As Integer = (execRc Shr 8) And 255
    If exitCode = 0 Then
      Return 1
    End If
    Return exitCode
  End If
  Return 0
End Function
