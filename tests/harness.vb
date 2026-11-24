' Shared pure-VerBose test harness for compiler integration, CLI, example & error-check suites.

Declare Function popen(cmd, mode As Pointer) As Pointer
Declare Function pclose(fp As Pointer) As Integer
Declare Function fread(buf As Pointer, sz, cnt As Long, fp As Pointer) As Long
Declare Function fclose(fp As Pointer) As Integer
Declare Function fprintf(fp, fmt As Pointer, ...) As Integer
Declare Function strstr(haystack, needle As Pointer) As Pointer
Declare Function system(cmd As Pointer) As Integer
Declare Function unlink(path As Pointer) As Integer
Declare Function getpid() As Integer
Declare Function snprintf(buf As Pointer, sz As Long, fmt As Pointer, ...) As Integer
Declare Function strlen(s As Pointer) As Long
Declare Function strcmp(a, b As Pointer) As Integer
Declare Function getenv(name As Pointer) As Pointer
Declare Function getcwd(buf As Pointer, sz As Long) As Pointer

Public Static g_out(131072) As Byte, g_outLen = 0, g_exitCode = 0 As Integer

Public Function IsWindowsHost() As Boolean
  Dim envOs As Pointer = getenv("OS")
  If Not IsNull(envOs) Then
    If strcmp(envOs, "Windows_NT") = 0 Then
      Return True
    End If
  End If
  Return False
End Function

Public Function GetTempDir() As Pointer
  If IsWindowsHost() Then
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

Public Sub GetCompilerAbs(dst As Pointer, cap As Integer)
  Dim envC As Pointer = getenv("VERBOSE_COMPILER")
  Dim raw As Pointer = "./verbose"
  If IsWindowsHost() Then
    raw = "verbose.exe"
  End If
  If Not IsNull(envC) Then
    If envC(0) <> 0 Then
      raw = envC
    End If
  End If
  If (raw(0) = 47) Or (raw(0) = 92) Or ((raw(0) <> 0) And (raw(1) = 58)) Then
    snprintf(dst, cap, "%s", raw)
    Return
  End If
  Dim cwdBuf(512) As Byte
  getcwd(cwdBuf, 512)
  If IsWindowsHost() Then
    snprintf(dst, cap, "%s\\%s", cwdBuf, raw)
  Else
    snprintf(dst, cap, "%s/%s", cwdBuf, raw)
  End If
End Sub

Public Function RunShell(cmd As Pointer) As Integer
  Dim fullCmd(4096) As Byte
  snprintf(fullCmd, 4096, "%s 2>&1", cmd)
  Dim fp As Pointer = popen(fullCmd, "r")
  If IsNull(fp) Then
    g_out(0) = 0
    g_outLen = 0
    g_exitCode = 1
    Return 1
  End If

  Dim total As Integer = 0
  Dim chunk(4096) As Byte
  Dim reading As Boolean = True
  While reading Do
    Dim n As Long = fread(chunk, 1, 4096, fp)
    Dim ni As Integer = n
    If ni <= 0 Then
      reading = False
    Else
      Dim i As Integer = 0
      While (i < ni) And (total < 131070) Do
        g_out(total) = chunk(i)
        total = total + 1
        i = i + 1
      End While
    End If
  End While
  g_out(total) = 0
  g_outLen = total

  Dim st As Integer = pclose(fp)
  If st <> 0 Then
    If IsWindowsHost() Then
      g_exitCode = st
    Else
      Dim ec As Integer = (st Shr 8) And 255
      If ec = 0 Then
        g_exitCode = 1
      Else
        g_exitCode = ec
      End If
    End If
  Else
    g_exitCode = 0
  End If
  Return g_exitCode
End Function

Public Function OutHas(needle As Pointer) As Boolean
  Return Not IsNull(strstr(g_out, needle))
End Function

Public Function FileExists(path As Pointer) As Boolean
  Dim fp As Pointer = Fopen(path, "rb")
  If IsNull(fp) Then
    Return False
  End If
  fclose(fp)
  Return True
End Function

Public Function FileHas(path, needle As Pointer) As Boolean
  Dim fp As Pointer = Fopen(path, "rb")
  If IsNull(fp) Then
    Return False
  End If
  Dim buf(65536) As Byte
  Dim n As Long = fread(buf, 1, 65535, fp)
  fclose(fp)
  Dim ni As Integer = n
  If ni < 0 Then
    ni = 0
  End If
  buf(ni) = 0
  Return Not IsNull(strstr(buf, needle))
End Function

Public Function WriteTextFile(path, text As Pointer) As Boolean
  Dim fp As Pointer = Fopen(path, "wb")
  If IsNull(fp) Then
    Return False
  End If
  fprintf(fp, "%s", text)
  fclose(fp)
  Return True
End Function

Public Function RunVb(vbPath, extraArgs As Pointer) As Integer
  Dim comp(768) As Byte
  GetCompilerAbs(comp, 768)

  Dim pid As Integer = getpid()
  Dim outBin(256) As Byte
  If IsWindowsHost() Then
    snprintf(outBin, 256, "%s\\vb_case_%d.exe", GetTempDir(), pid)
  Else
    snprintf(outBin, 256, "%s/vb_case_%d", GetTempDir(), pid)
  End If

  Dim llPath(512) As Byte
  Dim slen As Integer = strlen(vbPath)
  Dim copyLen As Integer = slen
  If (slen >= 3) And (vbPath(slen - 3) = 46) And (vbPath(slen - 2) = 118) And (vbPath(slen - 1) = 98) Then
    copyLen = slen - 3
  End If
  Dim i As Integer = 0
  While (i < copyLen) And (i < 508) Do
    llPath(i) = vbPath(i)
    i = i + 1
  End While
  llPath(i) = 46
  llPath(i + 1) = 108
  llPath(i + 2) = 108
  llPath(i + 3) = 0

  Dim cmd(2048) As Byte
  If (Not IsNull(extraArgs)) And (extraArgs(0) <> 0) Then
    snprintf(cmd, 2048, "\"%s\" \"%s\" -o \"%s\" %s", comp, vbPath, outBin, extraArgs)
  Else
    snprintf(cmd, 2048, "\"%s\" \"%s\" -o \"%s\"", comp, vbPath, outBin)
  End If

  Dim rc As Integer = RunShell(cmd)
  unlink(outBin)
  unlink(llPath)
  Return rc
End Function
