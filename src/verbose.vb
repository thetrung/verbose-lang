' VerBose Self-Hosting Compiler (verbose) - CLI Driver
' Compiles multi-file VerBose-Lang programs (.vb) directly into LLVM IR (.ll).
' Written in VerBose-Lang and capable of compiling itself (Stage 1 -> Stage 2 -> Stage 3 fixpoint).

Include "codegen.vb"

Public Function main(argc As Integer, argv() As Pointer) As Integer
  If argc < 3 Then
    printf("Usage: verbose <input.vb> <output.ll>\n")
    Return 1
  End If

  Dim inPath As Pointer = argv(1)
  Dim outPath As Pointer = argv(2)

  ' 16 MiB deterministic Arena for all source pools, tokens, AST nodes, and symbol tables
  Dim arena As Arena = ArenaCreate(16777216)
  Dim tokCount As Integer = LexerLoadEntry(ByRef arena, inPath)
  If tokCount < 0 Then
    ArenaDestroy(ByRef arena)
    Return 1
  End If

  AstInit(ByRef arena)
  Dim progRoot As Integer = ParseProgram()
  If g_nodes(progRoot).op > 0 Then
    printf("verbose: aborting due to %d parse error(s)\n", g_nodes(progRoot).op)
    ArenaDestroy(ByRef arena)
    Return 1
  End If

  Dim rc As Integer = EmitProgramToFile(ByRef arena, progRoot, outPath)
  Dim usedBytes As Long = ArenaUsed(ByRef arena)
  ArenaDestroy(ByRef arena)

  If rc <> 0 Then
    Return 1
  End If

  printf("verbose: compiled %s -> %s (bytes=%d tokens=%d nodes=%d arena=%ld)\n", inPath, outPath, g_srcPoolLen, tokCount, g_nodeCount, usedBytes)
  Return 0
End Function
