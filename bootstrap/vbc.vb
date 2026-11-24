' VerBose Bootstrap Compiler (vbc) - CLI Driver
' Compiles a VerBose-Lang source file (.vb) directly into LLVM IR (.ll)
' Built entirely in VerBose-Lang using Include, Arena, Array, Structure, Enum, ByRef, Select Case, and Declare FFI!

Include "codegen.vb"

Declare Function fseek(fp As Pointer, offset As Long, whence As Integer) As Integer
Declare Function ftell(fp As Pointer) As Long
Declare Function fread(buf As Pointer, size As Long, count As Long, fp As Pointer) As Long

Public Function main(argc As Integer, argv() As Pointer) As Integer
  If argc < 3 Then
    printf("Usage: vbc <input.vb> <output.ll>\n")
    Return 1
  End If

  Dim inPath As Pointer = argv(1)
  Dim outPath As Pointer = argv(2)

  Dim fp As Pointer = fopen(inPath, "rb")
  If IsNull(fp) Then
    printf("vbc error: cannot open input file '%s'\n", inPath)
    Return 1
  End If

  fseek(fp, 0, 2)
  Dim fsizeLong As Long = ftell(fp)
  fseek(fp, 0, 0)
  Dim fsize As Integer = fsizeLong

  ' Single deterministic Arena for source buffer + token stream + AST pool
  Dim arena As Arena = ArenaCreate(1048576)
  Dim srcBuf As Pointer = ArenaAllocBytes(ByRef arena, fsize + 1)
  If IsNull(srcBuf) Then
    printf("vbc error: arena out of memory\n")
    fclose(fp)
    ArenaDestroy(ByRef arena)
    Return 1
  End If

  fread(srcBuf, 1, fsizeLong, fp)
  fclose(fp)

  Dim tokCount As Integer = TokenizeAll(ByRef arena, srcBuf, fsize)
  If tokCount < 0 Then
    ArenaDestroy(ByRef arena)
    Return 1
  End If

  AstInit(ByRef arena)
  Dim progRoot As Integer = ParseProgram()
  Dim rc As Integer = EmitProgramToFile(progRoot, outPath)
  Dim usedBytes As Long = ArenaUsed(ByRef arena)
  ArenaDestroy(ByRef arena)

  If rc <> 0 Then
    Return 1
  End If

  printf("vbc: compiled %s -> %s (bytes=%d tokens=%d nodes=%d arena=%ld)\n", inPath, outPath, fsize, tokCount, g_nodeCount, usedBytes)
  Return 0
End Function
