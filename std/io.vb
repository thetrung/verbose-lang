' VerBose Standard Library: Arena-backed File & Console I/O (std/io.vb)

Declare Function fclose(fp As Pointer) As Integer
Declare Function fseek(fp As Pointer, offset As Long, whence As Integer) As Integer
Declare Function ftell(fp As Pointer) As Long
Declare Function fread(buf As Pointer, size, count As Long, fp As Pointer) As Long
Declare Function fwrite(buf As Pointer, size, count As Long, fp As Pointer) As Long
Declare Function remove(path As Pointer) As Integer
Declare Function strlen(s As Pointer) As Long
Declare Function fflush(fp As Pointer) As Integer

Public Structure IoBuf
  Dim data As Pointer
  Dim length As Integer
  Dim ok As Boolean
End Structure

Private Function IoFailBuf() As IoBuf
  Dim b As IoBuf With ( data = 0, length = 0, ok = False )
  Return b
End Function

Public Function IoReadFile(ByRef a As Arena, path As Pointer) As IoBuf
  If IsNull(path) Then
    Return IoFailBuf()
  End If

  Dim fp As Pointer = Fopen(path, "rb")
  If IsNull(fp) Then
    Return IoFailBuf()
  End If

  If fseek(fp, 0, 2) <> 0 Then
    fclose(fp)
    Return IoFailBuf()
  End If

  Dim fsizeLong As Long = ftell(fp)
  If fsizeLong < 0 Then
    fclose(fp)
    Return IoFailBuf()
  End If

  If fseek(fp, 0, 0) <> 0 Then
    fclose(fp)
    Return IoFailBuf()
  End If

  Dim fsize As Integer = fsizeLong
  Dim buf As Pointer = ArenaAllocBytes(ByRef a, fsize + 1)
  If IsNull(buf) Then
    fclose(fp)
    Return IoFailBuf()
  End If

  If fsize > 0 Then
    Dim nread As Long = fread(buf, 1, fsizeLong, fp)
    If nread < 0 Then
      fclose(fp)
      Return IoFailBuf()
    End If
    If nread < fsizeLong Then
      fsize = nread
    End If
  End If

  buf(fsize) = 0
  fclose(fp)

  Dim res As IoBuf With ( data = buf, length = fsize, ok = True )
  Return res
End Function

Public Function IoWriteFile(path, data As Pointer, length As Integer) As Boolean
  If IsNull(path) Then
    Return False
  End If
  If length < 0 Then
    Return False
  End If
  If length > 0 Then
    If IsNull(data) Then
      Return False
    End If
  End If

  Dim fp As Pointer = Fopen(path, "wb")
  If IsNull(fp) Then
    Return False
  End If

  Dim ok As Boolean = True
  If length > 0 Then
    Dim want As Long = length
    Dim written As Long = fwrite(data, 1, want, fp)
    If written <> want Then
      ok = False
    End If
  End If

  If fclose(fp) <> 0 Then
    ok = False
  End If
  Return ok
End Function

Private Function IoWriteMode(path, text, mode As Pointer) As Boolean
  If IsNull(path) Then
    Return False
  End If
  If IsNull(text) Then
    Return False
  End If

  Dim fp As Pointer = Fopen(path, mode)
  If IsNull(fp) Then
    Return False
  End If

  Dim slen As Long = strlen(text)
  Dim ok As Boolean = True
  If slen > 0 Then
    Dim written As Long = fwrite(text, 1, slen, fp)
    If written <> slen Then
      ok = False
    End If
  End If

  If fclose(fp) <> 0 Then
    ok = False
  End If
  Return ok
End Function

Public Function IoWriteText(path, text As Pointer) As Boolean
  Return IoWriteMode(path, text, "wb")
End Function

Public Function IoAppendText(path, text As Pointer) As Boolean
  Return IoWriteMode(path, text, "ab")
End Function

Public Function IoFileExists(path As Pointer) As Boolean
  If IsNull(path) Then
    Return False
  End If
  Dim fp As Pointer = Fopen(path, "rb")
  If IsNull(fp) Then
    Return False
  End If
  fclose(fp)
  Return True
End Function

Public Function IoFileSize(path As Pointer) As Long
  If IsNull(path) Then
    Return -1
  End If
  Dim fp As Pointer = Fopen(path, "rb")
  If IsNull(fp) Then
    Return -1
  End If
  If fseek(fp, 0, 2) <> 0 Then
    fclose(fp)
    Return -1
  End If
  Dim sz As Long = ftell(fp)
  fclose(fp)
  Return sz
End Function

Public Function IoRemoveFile(path As Pointer) As Boolean
  If IsNull(path) Then
    Return False
  End If
  Return remove(path) = 0
End Function

Public Sub IoPrint(s As Pointer)
  If IsNull(s) Then
    Return
  End If
  Printf("%s", s)
  fflush(0)
End Sub

Public Sub IoPrintLn(s As Pointer)
  If Not IsNull(s) Then
    Printf("%s\n", s)
  Else
    Printf("\n")
  End If
  fflush(0)
End Sub

Public Function IoReadLine(ByRef a As Arena, fp As Pointer) As IoBuf
  If IsNull(fp) Then
    Return IoFailBuf()
  End If

  Dim c As Integer = Getc(fp)
  If c < 0 Then
    Return IoFailBuf()
  End If

  Dim stackBuf(4096) As Byte
  Dim curBuf As Pointer = stackBuf
  Dim curCap As Integer = 4096
  Dim length As Integer = 0
  Dim inArena As Boolean = False

  Dim reading As Boolean = True
  While reading Do
    If c < 0 Then
      reading = False
    Else
      If c = 10 Then
        reading = False
      Else
        If (length + 1) >= curCap Then
          Dim newCap As Integer = curCap * 2
          Dim nextBuf As Pointer = ArenaAllocBytes(ByRef a, newCap)
          If IsNull(nextBuf) Then
            Return IoFailBuf()
          End If
          Dim i As Integer = 0
          While i < length Do
            nextBuf(i) = curBuf(i)
            i = i + 1
          End While
          curBuf = nextBuf
          curCap = newCap
          inArena = True
        End If
        curBuf(length) = c
        length = length + 1
        c = Getc(fp)
      End If
    End If
  End While

  If length > 0 Then
    Dim last As Integer = curBuf(length - 1) And 255
    If last = 13 Then
      length = length - 1
    End If
  End If

  If Not inArena Then
    Dim outBuf As Pointer = ArenaAllocBytes(ByRef a, length + 1)
    If IsNull(outBuf) Then
      Return IoFailBuf()
    End If
    Dim j As Integer = 0
    While j < length Do
      outBuf(j) = curBuf(j)
      j = j + 1
    End While
    curBuf = outBuf
  End If

  curBuf(length) = 0
  Dim res As IoBuf With ( data = curBuf, length = length, ok = True )
  Return res
End Function
