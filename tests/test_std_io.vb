' Pure-VerBose test suite for std/io.vb

Include "../std/io.vb"

Declare Function getpid() As Integer
Declare Function snprintf(buf As Pointer, sz As Long, fmt As Pointer, ...) As Integer
Declare Function strcmp(a, b As Pointer) As Integer

Private Sub MakeTmpPath(dst As Pointer, cap As Integer, tag As Pointer)
  Dim pid As Integer = getpid()
  snprintf(dst, cap, "/tmp/vb_std_io_%d_%s", pid, tag)
End Sub

Test "IoWriteText, IoFileExists, IoFileSize, IoReadFile, IoRemoveFile"
  Dim path(256) As Byte
  MakeTmpPath(path, 256, "basic.txt")
  IoRemoveFile(path)

  Assert Not IoFileExists(path), "file should not exist before write"
  Assert IoFileSize(path) = -1, "missing file size should be -1"

  Assert IoWriteText(path, "hello verbose"), "IoWriteText should succeed"
  Assert IoFileExists(path), "file should exist after write"
  Assert IoFileSize(path) = 13, "file size should be 13 bytes"

  Dim a As Arena = ArenaCreate(1024)
  Dim buf As IoBuf = IoReadFile(ByRef a, path)
  Assert buf.ok, "IoReadFile should succeed"
  Assert buf.length = 13, "IoReadFile length should be 13"
  Assert strcmp(buf.data, "hello verbose") = 0, "IoReadFile content mismatch"

  ArenaDestroy(ByRef a)
  Assert IoRemoveFile(path), "IoRemoveFile should succeed"
  Assert Not IoFileExists(path), "file should not exist after remove"
  Assert Not IoRemoveFile(path), "removing missing file should return False"
End Test

Test "IoAppendText and empty file handling"
  Dim path(256) As Byte
  MakeTmpPath(path, 256, "append.txt")
  IoRemoveFile(path)

  Assert IoWriteText(path, ""), "writing empty text should succeed"
  Assert IoFileExists(path), "empty file should exist"
  Assert IoFileSize(path) = 0, "empty file size should be 0"

  Dim a As Arena = ArenaCreate(1024)
  Dim emptyBuf As IoBuf = IoReadFile(ByRef a, path)
  Assert emptyBuf.ok, "reading empty file should succeed"
  Assert emptyBuf.length = 0, "empty file length should be 0"
  Assert (emptyBuf.data(0) And 255) = 0, "empty file buffer must be null-terminated"

  Assert IoAppendText(path, "first"), "first append should succeed"
  Assert IoAppendText(path, "-second"), "second append should succeed"
  Assert IoFileSize(path) = 12, "appended file size should be 12"

  Dim fullBuf As IoBuf = IoReadFile(ByRef a, path)
  Assert fullBuf.ok, "reading appended file should succeed"
  Assert fullBuf.length = 12, "appended file length should be 12"
  Assert strcmp(fullBuf.data, "first-second") = 0, "appended content mismatch"

  ArenaDestroy(ByRef a)
  Assert IoRemoveFile(path)
End Test

Test "IoWriteFile binary data with embedded zero byte"
  Dim path(256) As Byte
  MakeTmpPath(path, 256, "bin.dat")
  IoRemoveFile(path)

  Dim raw(5) As Byte
  raw(0) = 65
  raw(1) = 0
  raw(2) = 255
  raw(3) = 66
  raw(4) = 10

  Assert IoWriteFile(path, raw, 5), "IoWriteFile binary should succeed"
  Assert IoFileSize(path) = 5, "binary file size should be 5"

  Dim a As Arena = ArenaCreate(1024)
  Dim buf As IoBuf = IoReadFile(ByRef a, path)
  Assert buf.ok, "IoReadFile binary should succeed"
  Assert buf.length = 5, "binary read length should be 5"
  Assert (buf.data(0) And 255) = 65
  Assert (buf.data(1) And 255) = 0
  Assert (buf.data(2) And 255) = 255
  Assert (buf.data(3) And 255) = 66
  Assert (buf.data(4) And 255) = 10
  Assert (buf.data(5) And 255) = 0, "trailing null terminator required"

  ArenaDestroy(ByRef a)
  Assert IoRemoveFile(path)
End Test

Test "IoReadLine with LF, CRLF, empty line, and no-newline EOF"
  Dim path(256) As Byte
  MakeTmpPath(path, 256, "lines.txt")
  IoRemoveFile(path)

  Assert IoWriteText(path, "alpha\nbeta\r\n\ngamma")

  Dim fp As Pointer = Fopen(path, "rb")
  Assert Not IsNull(fp), "Fopen should succeed"

  Dim a As Arena = ArenaCreate(2048)

  Dim l1 As IoBuf = IoReadLine(ByRef a, fp)
  Assert l1.ok
  Assert l1.length = 5
  Assert strcmp(l1.data, "alpha") = 0

  Dim l2 As IoBuf = IoReadLine(ByRef a, fp)
  Assert l2.ok
  Assert l2.length = 4
  Assert strcmp(l2.data, "beta") = 0

  Dim l3 As IoBuf = IoReadLine(ByRef a, fp)
  Assert l3.ok
  Assert l3.length = 0
  Assert strcmp(l3.data, "") = 0

  Dim l4 As IoBuf = IoReadLine(ByRef a, fp)
  Assert l4.ok
  Assert l4.length = 5
  Assert strcmp(l4.data, "gamma") = 0

  Dim l5 As IoBuf = IoReadLine(ByRef a, fp)
  Assert Not l5.ok, "immediate EOF must return ok = False"
  Assert l5.length = 0

  fclose(fp)
  ArenaDestroy(ByRef a)
  Assert IoRemoveFile(path)
End Test

Test "IoReadLine long line exceeding 4096-byte stack buffer"
  Dim path(256) As Byte
  MakeTmpPath(path, 256, "longline.txt")
  IoRemoveFile(path)

  Dim big(5001) As Byte
  Dim i As Integer = 0
  While i < 5000 Do
    Dim ch As Integer = 65 + (i Mod 26)
    big(i) = ch
    i = i + 1
  End While
  big(5000) = 10
  Assert IoWriteFile(path, big, 5001)

  Dim fp As Pointer = Fopen(path, "rb")
  Assert Not IsNull(fp)
  Dim a As Arena = ArenaCreate(32768)
  Dim line As IoBuf = IoReadLine(ByRef a, fp)
  Assert line.ok, "long IoReadLine should succeed"
  Assert line.length = 5000, "long IoReadLine length should be 5000"
  Dim b0 As Integer = line.data(0)
  Dim bLast As Integer = line.data(4999)
  Dim bEnd As Integer = line.data(5000)
  Assert (b0 And 255) = 65
  Assert (bLast And 255) = (65 + (4999 Mod 26))
  Assert (bEnd And 255) = 0

  Dim eofLine As IoBuf = IoReadLine(ByRef a, fp)
  Assert Not eofLine.ok

  fclose(fp)
  ArenaDestroy(ByRef a)
  Assert IoRemoveFile(path)
End Test

Test "IoReadFile missing file and OOM errors"
  Dim missing(256) As Byte
  MakeTmpPath(missing, 256, "does_not_exist.txt")
  IoRemoveFile(missing)

  Dim a As Arena = ArenaCreate(64)
  Dim bMiss As IoBuf = IoReadFile(ByRef a, missing)
  Assert Not bMiss.ok, "missing file must return ok = False"
  Assert IsNull(bMiss.data), "missing file data must be null"
  Assert bMiss.length = 0

  Dim nullPath As Pointer = 0
  Assert Not IoFileExists(nullPath)
  Assert IoFileSize(nullPath) = -1
  Assert Not IoRemoveFile(nullPath)
  Assert Not IoWriteText(nullPath, "x")
  Assert Not IoAppendText(nullPath, "x")
  Assert Not IoWriteFile(nullPath, "x", 1)

  Dim path(256) As Byte
  MakeTmpPath(path, 256, "oom.txt")
  Assert IoWriteText(path, "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789")
  Dim tiny As Arena = ArenaCreate(16)
  Dim bOom As IoBuf = IoReadFile(ByRef tiny, path)
  Assert Not bOom.ok, "OOM arena must return ok = False"

  ArenaDestroy(ByRef a)
  ArenaDestroy(ByRef tiny)
  Assert IoRemoveFile(path)
End Test

Test "IoPrint and IoPrintLn smoke"
  IoPrint("io_print_")
  IoPrintLn("ok")
  IoPrintLn(0)
  Assert True
End Test
