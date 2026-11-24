' VerBose Self-Hosting Compiler (verbose) - Multi-File Lexer & Include Module Loader

Include "token.vb"
Include "constants.vb"

Declare Function fclose(fp As Pointer) As Integer
Declare Function fseek(fp As Pointer, offset As Long, whence As Integer) As Integer
Declare Function ftell(fp As Pointer) As Long
Declare Function fread(buf As Pointer, size As Long, count As Long, fp As Pointer) As Long

Public Static g_srcPool As Pointer, g_srcPoolLen, g_srcPoolCap As Integer
Public Static g_tokens() As Token, g_tokCount, g_tokCap As Integer

Structure LoadedFile
  Dim pathPos, pathLen, inProgress As Integer
End Structure

Private Static lx_loaded(64) As LoadedFile, lx_incDirs(32) As Pointer
Private Static lx_loadedCount = 0, lx_curModId = 0, lx_incDirCount = 0 As Integer

Public Sub LexerClearIncludeDirs()
  lx_incDirCount = 0
End Sub

Public Sub LexerAddIncludeDir(dirPtr As Pointer)
  If lx_incDirCount < 32 Then
    lx_incDirs(lx_incDirCount) = dirPtr
    lx_incDirCount = lx_incDirCount + 1
  End If
End Sub

Private Function LxIsAlpha(c As Integer) As Boolean
  If (c >= 65) And (c <= 90) Then
    Return True
  End If
  If (c >= 97) And (c <= 122) Then
    Return True
  End If
  If c = 95 Then
    Return True
  End If
  Return False
End Function

Private Function LxIsDigit(c As Integer) As Boolean
  Return (c >= 48) And (c <= 57)
End Function

Private Function LxIsHexDigit(c As Integer) As Boolean
  If LxIsDigit(c) Then
    Return True
  End If
  If (c >= 65) And (c <= 70) Then
    Return True
  End If
  If (c >= 97) And (c <= 102) Then
    Return True
  End If
  Return False
End Function

Private Function LxHexVal(c As Integer) As Integer
  If (c >= 48) And (c <= 57) Then
    Return c - 48
  End If
  If (c >= 65) And (c <= 70) Then
    Return c - 55
  End If
  Return c - 87
End Function

Private Function LxIsAlnum(c As Integer) As Boolean
  Return LxIsAlpha(c) Or LxIsDigit(c)
End Function

Public Function SliceEqualsStr(pos, slen As Integer, lit As Pointer, litLen As Integer) As Boolean
  If slen <> litLen Then
    Return False
  End If
  Dim i As Integer = 0
  While i < slen Do
    Dim a = g_srcPool(pos + i), b = lit(i) As Integer
    If a <> b Then
      Return False
    End If
    i = i + 1
  End While
  Return True
End Function

Public Function SlicesEqual(p1, l1, p2, l2 As Integer) As Boolean
  If l1 <> l2 Then
    Return False
  End If
  Dim i As Integer = 0
  While i < l1 Do
    Dim a = g_srcPool(p1 + i), b = g_srcPool(p2 + i) As Integer
    If a <> b Then
      Return False
    End If
    i = i + 1
  End While
  Return True
End Function

Private Function LxClassifyKeyword(pos, slen As Integer) As Integer
  If SliceEqualsStr(pos, slen, "Include", 7) Then
    Return TokenKind.TkInclude
  End If
  If SliceEqualsStr(pos, slen, "Public", 6) Then
    Return TokenKind.TkPublic
  End If
  If SliceEqualsStr(pos, slen, "Private", 7) Then
    Return TokenKind.TkPrivate
  End If
  If SliceEqualsStr(pos, slen, "Declare", 7) Then
    Return TokenKind.TkDeclare
  End If
  If SliceEqualsStr(pos, slen, "Structure", 9) Then
    Return TokenKind.TkStructure
  End If
  If SliceEqualsStr(pos, slen, "Enum", 4) Then
    Return TokenKind.TkEnum
  End If
  If SliceEqualsStr(pos, slen, "Static", 6) Then
    Return TokenKind.TkStatic
  End If
  If SliceEqualsStr(pos, slen, "Function", 8) Then
    Return TokenKind.TkFunction
  End If
  If SliceEqualsStr(pos, slen, "Sub", 3) Then
    Return TokenKind.TkSub
  End If
  If SliceEqualsStr(pos, slen, "End", 3) Then
    Return TokenKind.TkEnd
  End If
  If SliceEqualsStr(pos, slen, "Dim", 3) Then
    Return TokenKind.TkDim
  End If
  If SliceEqualsStr(pos, slen, "As", 2) Then
    Return TokenKind.TkAs
  End If
  If SliceEqualsStr(pos, slen, "ByVal", 5) Then
    Return TokenKind.TkByVal
  End If
  If SliceEqualsStr(pos, slen, "ByRef", 5) Then
    Return TokenKind.TkByRef
  End If
  If SliceEqualsStr(pos, slen, "With", 4) Then
    Return TokenKind.TkWith
  End If
  If SliceEqualsStr(pos, slen, "Return", 6) Then
    Return TokenKind.TkReturn
  End If
  If SliceEqualsStr(pos, slen, "Test", 4) Then
    Return TokenKind.TkTest
  End If
  If SliceEqualsStr(pos, slen, "Assert", 6) Then
    Return TokenKind.TkAssert
  End If
  If SliceEqualsStr(pos, slen, "Volatile", 8) Then
    Return TokenKind.TkVolatile
  End If
  If SliceEqualsStr(pos, slen, "VolatileLoad", 12) Then
    Return TokenKind.TkVolatileLoad
  End If
  If SliceEqualsStr(pos, slen, "VolatileStore", 13) Then
    Return TokenKind.TkVolatileStore
  End If
  If SliceEqualsStr(pos, slen, "AddressOf", 9) Then
    Return TokenKind.TkAddressOf
  End If
  If SliceEqualsStr(pos, slen, "PtrRead", 7) Then
    Return TokenKind.TkPtrRead
  End If
  If SliceEqualsStr(pos, slen, "PtrWrite", 8) Then
    Return TokenKind.TkPtrWrite
  End If
  If SliceEqualsStr(pos, slen, "PtrAdd", 6) Then
    Return TokenKind.TkPtrAdd
  End If
  If SliceEqualsStr(pos, slen, "Label", 5) Then
    Return TokenKind.TkLabel
  End If
  If SliceEqualsStr(pos, slen, "GoTo", 4) Then
    Return TokenKind.TkGoTo
  End If
  If SliceEqualsStr(pos, slen, "Async", 5) Then
    Return TokenKind.TkAsync
  End If
  If SliceEqualsStr(pos, slen, "Await", 5) Then
    Return TokenKind.TkAwait
  End If
  If SliceEqualsStr(pos, slen, "Task", 4) Then
    Return TokenKind.TkTask
  End If
  If SliceEqualsStr(pos, slen, "Integer", 7) Then
    Return TokenKind.TkInteger
  End If
  If SliceEqualsStr(pos, slen, "Byte", 4) Then
    Return TokenKind.TkByte
  End If
  If SliceEqualsStr(pos, slen, "Short", 5) Then
    Return TokenKind.TkShort
  End If
  If SliceEqualsStr(pos, slen, "Long", 4) Then
    Return TokenKind.TkLong
  End If
  If SliceEqualsStr(pos, slen, "Boolean", 7) Then
    Return TokenKind.TkBoolean
  End If
  If SliceEqualsStr(pos, slen, "Pointer", 7) Then
    Return TokenKind.TkPointer
  End If
  If SliceEqualsStr(pos, slen, "Arena", 5) Then
    Return TokenKind.TkArena
  End If
  If SliceEqualsStr(pos, slen, "Nothing", 7) Then
    Return TokenKind.TkNothing
  End If
  If SliceEqualsStr(pos, slen, "Single", 6) Then
    Return TokenKind.TkSingle
  End If
  If SliceEqualsStr(pos, slen, "Double", 6) Then
    Return TokenKind.TkDouble
  End If
  If SliceEqualsStr(pos, slen, "True", 4) Then
    Return TokenKind.TkTrue
  End If
  If SliceEqualsStr(pos, slen, "False", 5) Then
    Return TokenKind.TkFalse
  End If
  If SliceEqualsStr(pos, slen, "SizeOf", 6) Then
    Return TokenKind.TkSizeOf
  End If
  If SliceEqualsStr(pos, slen, "ArenaAlloc", 10) Then
    Return TokenKind.TkArenaAlloc
  End If
  If SliceEqualsStr(pos, slen, "HeapAlloc", 9) Then
    Return TokenKind.TkHeapAlloc
  End If
  If SliceEqualsStr(pos, slen, "HeapRealloc", 11) Then
    Return TokenKind.TkHeapRealloc
  End If
  If SliceEqualsStr(pos, slen, "If", 2) Then
    Return TokenKind.TkIf
  End If
  If SliceEqualsStr(pos, slen, "Then", 4) Then
    Return TokenKind.TkThen
  End If
  If SliceEqualsStr(pos, slen, "Else", 4) Then
    Return TokenKind.TkElse
  End If
  If SliceEqualsStr(pos, slen, "While", 5) Then
    Return TokenKind.TkWhile
  End If
  If SliceEqualsStr(pos, slen, "Do", 2) Then
    Return TokenKind.TkDo
  End If
  If SliceEqualsStr(pos, slen, "For", 3) Then
    Return TokenKind.TkFor
  End If
  If SliceEqualsStr(pos, slen, "To", 2) Then
    Return TokenKind.TkTo
  End If
  If SliceEqualsStr(pos, slen, "Select", 6) Then
    Return TokenKind.TkSelect
  End If
  If SliceEqualsStr(pos, slen, "Case", 4) Then
    Return TokenKind.TkCase
  End If
  If SliceEqualsStr(pos, slen, "And", 3) Then
    Return TokenKind.TkAnd
  End If
  If SliceEqualsStr(pos, slen, "Or", 2) Then
    Return TokenKind.TkOr
  End If
  If SliceEqualsStr(pos, slen, "Not", 3) Then
    Return TokenKind.TkNot
  End If
  If SliceEqualsStr(pos, slen, "Mod", 3) Then
    Return TokenKind.TkMod
  End If
  If SliceEqualsStr(pos, slen, "Xor", 3) Then
    Return TokenKind.TkXor
  End If
  If SliceEqualsStr(pos, slen, "Shl", 3) Then
    Return TokenKind.TkShl
  End If
  If SliceEqualsStr(pos, slen, "Shr", 3) Then
    Return TokenKind.TkShr
  End If
  Return TokenKind.TkIdent
End Function

Private Sub LxEmitTok(kind, pos, slen, ival, line As Integer)
  If g_tokCount < g_tokCap Then
    g_tokens(g_tokCount) = Token With (
      kind = kind,
      pos = pos,
      len = slen,
      intVal = ival,
      line = line,
      modId = lx_curModId
    )
    g_tokCount = g_tokCount + 1
  End If
End Sub

Private Function LxFindLoaded(pPos, pLen As Integer) As Integer
  Dim i As Integer = 0
  While i < lx_loadedCount Do
    If SlicesEqual(lx_loaded(i).pathPos, lx_loaded(i).pathLen, pPos, pLen) Then
      Return i
    End If
    i = i + 1
  End While
  Return -1
End Function

Private Function LxFileExists(pPos, pLen As Integer) As Boolean
  Dim pathBuf(512) As Byte
  Dim pi As Integer = 0
  While (pi < pLen) And (pi < 511) Do
    pathBuf(pi) = g_srcPool(pPos + pi)
    pi = pi + 1
  End While
  pathBuf(pi) = 0
  Dim fp As Pointer = Fopen(pathBuf, "rb")
  If IsNull(fp) Then
    Return False
  End If
  fclose(fp)
  Return True
End Function

Private Function LxCopyCStringToPool(s As Pointer) As Integer
  Dim startPos As Integer = g_srcPoolLen
  Dim i As Integer = 0
  Dim going As Boolean = True
  While going Do
    Dim ch As Integer = s(i)
    If ch = 0 Then
      going = False
    Else
      g_srcPool(g_srcPoolLen) = ch
      g_srcPoolLen = g_srcPoolLen + 1
      i = i + 1
    End If
  End While
  g_srcPool(g_srcPoolLen) = 0
  g_srcPoolLen = g_srcPoolLen + 1
  Return startPos
End Function

' Canonicalize path (collapse '.' and '..') into g_srcPool
Private Function LxNormalizePath(rawPos, rawLen As Integer, ByRef outLen As Integer) As Integer
  Dim segPos(64), segLen(64) As Integer
  Dim segCnt = 0 As Integer, isAbs = False As Boolean
  If (rawLen > 0) And (g_srcPool(rawPos) = 47) Then
    isAbs = True
  End If
  Dim i As Integer = 0
  While i < rawLen Do
    While (i < rawLen) And (g_srcPool(rawPos + i) = 47) Do
      i = i + 1
    End While
    Dim sStart As Integer = i
    While (i < rawLen) And (g_srcPool(rawPos + i) <> 47) Do
      i = i + 1
    End While
    Dim sLen As Integer = i - sStart
    If sLen > 0 Then
      Dim isDot As Boolean = (sLen = 1) And (g_srcPool(rawPos + sStart) = 46)
      Dim isDotDot As Boolean = (sLen = 2) And (g_srcPool(rawPos + sStart) = 46) And (g_srcPool(rawPos + sStart + 1) = 46)
      If Not isDot Then
        If isDotDot Then
          Dim canPop As Boolean = False
          If segCnt > 0 Then
            Dim topLen As Integer = segLen(segCnt - 1)
            Dim topPos As Integer = segPos(segCnt - 1)
            Dim topIsDD As Boolean = (topLen = 2) And (g_srcPool(topPos) = 46) And (g_srcPool(topPos + 1) = 46)
            If Not topIsDD Then
              canPop = True
            End If
          End If
          If canPop Then
            segCnt = segCnt - 1
          Else
            If Not isAbs Then
              segPos(segCnt) = rawPos + sStart
              segLen(segCnt) = sLen
              segCnt = segCnt + 1
            End If
          End If
        Else
          segPos(segCnt) = rawPos + sStart
          segLen(segCnt) = sLen
          segCnt = segCnt + 1
        End If
      End If
    End If
  End While

  Dim startPos As Integer = g_srcPoolLen
  If isAbs Then
    g_srcPool(g_srcPoolLen) = 47
    g_srcPoolLen = g_srcPoolLen + 1
  End If
  Dim k As Integer = 0
  While k < segCnt Do
    If k > 0 Then
      g_srcPool(g_srcPoolLen) = 47
      g_srcPoolLen = g_srcPoolLen + 1
    End If
    Dim j As Integer = 0
    While j < segLen(k) Do
      g_srcPool(g_srcPoolLen) = g_srcPool(segPos(k) + j)
      g_srcPoolLen = g_srcPoolLen + 1
      j = j + 1
    End While
    k = k + 1
  End While
  outLen = g_srcPoolLen - startPos
  g_srcPool(g_srcPoolLen) = 0
  g_srcPoolLen = g_srcPoolLen + 1
  Return startPos
End Function

' Resolve relPos..relPos+relLen relative to curPathPos..curPathPos+curPathLen into g_srcPool
Private Function LxResolveRelPath(curPos, curLen, relPos, relLen As Integer, ByRef outLen As Integer) As Integer
  Dim startPos As Integer = g_srcPoolLen
  Dim firstRel As Integer = 0
  If relLen > 0 Then
    firstRel = g_srcPool(relPos)
  End If
  If firstRel <> 47 Then
    ' Find last '/' in curPath
    Dim lastSlash As Integer = -1
    Dim i As Integer = 0
    While i < curLen Do
      If g_srcPool(curPos + i) = 47 Then
        lastSlash = i
      End If
      i = i + 1
    End While
    If lastSlash >= 0 Then
      Dim j As Integer = 0
      While j <= lastSlash Do
        g_srcPool(g_srcPoolLen) = g_srcPool(curPos + j)
        g_srcPoolLen = g_srcPoolLen + 1
        j = j + 1
      End While
    End If
  End If
  Dim k As Integer = 0
  While k < relLen Do
    g_srcPool(g_srcPoolLen) = g_srcPool(relPos + k)
    g_srcPoolLen = g_srcPoolLen + 1
    k = k + 1
  End While
  Dim rawLen As Integer = g_srcPoolLen - startPos
  g_srcPool(g_srcPoolLen) = 0
  g_srcPoolLen = g_srcPoolLen + 1
  Return LxNormalizePath(startPos, rawLen, ByRef outLen)
End Function

Private Function LxResolveIncludePath(curPos, curLen, incPos, incLen As Integer, ByRef outLen As Integer) As Integer
  Dim candLen As Integer = 0
  Dim candPos As Integer = LxResolveRelPath(curPos, curLen, incPos, incLen, ByRef candLen)
  If LxFileExists(candPos, candLen) Then
    outLen = candLen
    Return candPos
  End If

  ' Try -I search directories in order
  Dim d As Integer = 0
  While d < lx_incDirCount Do
    Dim dirPtr As Pointer = lx_incDirs(d)
    Dim startPos As Integer = g_srcPoolLen
    Dim j As Integer = 0
    Dim reading As Boolean = True
    While reading Do
      Dim ch As Integer = dirPtr(j)
      If ch = 0 Then
        reading = False
      Else
        g_srcPool(g_srcPoolLen) = ch
        g_srcPoolLen = g_srcPoolLen + 1
        j = j + 1
      End If
    End While
    If (g_srcPoolLen > startPos) And (g_srcPool(g_srcPoolLen - 1) <> 47) Then
      g_srcPool(g_srcPoolLen) = 47
      g_srcPoolLen = g_srcPoolLen + 1
    End If
    Dim k As Integer = 0
    While k < incLen Do
      g_srcPool(g_srcPoolLen) = g_srcPool(incPos + k)
      g_srcPoolLen = g_srcPoolLen + 1
      k = k + 1
    End While
    Dim rawLen As Integer = g_srcPoolLen - startPos
    g_srcPool(g_srcPoolLen) = 0
    g_srcPoolLen = g_srcPoolLen + 1
    Dim normLen As Integer = 0
    Dim normPos As Integer = LxNormalizePath(startPos, rawLen, ByRef normLen)
    If LxFileExists(normPos, normLen) Then
      outLen = normLen
      Return normPos
    End If
    d = d + 1
  End While

  ' Lowest-priority fallback: resolve relative to current working directory (.) so Include "std/..." works from subdirs
  Dim cwdLen As Integer = 0
  Dim cwdPos As Integer = LxNormalizePath(incPos, incLen, ByRef cwdLen)
  If LxFileExists(cwdPos, cwdLen) Then
    outLen = cwdLen
    Return cwdPos
  End If

  Dim incBuf(512) As Byte
  Dim ii As Integer = 0
  While (ii < incLen) And (ii < 511) Do
    incBuf(ii) = g_srcPool(incPos + ii)
    ii = ii + 1
  End While
  incBuf(ii) = 0
  Printf(MSG_LEX_INC_NOT_FOUND, incBuf)
  Return -1
End Function

Public Function LexerLoadFile(ByRef arena As Arena, pathPos, pathLen As Integer) As Integer
  Dim loadedIdx As Integer = LxFindLoaded(pathPos, pathLen)
  If loadedIdx >= 0 Then
    If lx_loaded(loadedIdx).inProgress = 1 Then
      Dim cycBuf(512) As Byte
      Dim ci2 As Integer = 0
      While (ci2 < pathLen) And (ci2 < 511) Do
        cycBuf(ci2) = g_srcPool(pathPos + ci2)
        ci2 = ci2 + 1
      End While
      cycBuf(ci2) = 0
      Printf(MSG_LEX_CIRCULAR_INC, cycBuf)
      Return -1
    End If
    Return 0
  End If
  Dim savedModId As Integer = lx_curModId
  Dim mySlot As Integer = lx_loadedCount
  lx_curModId = mySlot
  lx_loaded(mySlot) = LoadedFile With ( pathPos = pathPos, pathLen = pathLen, inProgress = 1 )
  lx_loadedCount = lx_loadedCount + 1

  ' Copy path to small null-terminated stack buffer for Fopen
  Dim pathBuf(512) As Byte
  Dim pi As Integer = 0
  While pi < pathLen Do
    pathBuf(pi) = g_srcPool(pathPos + pi)
    pi = pi + 1
  End While
  pathBuf(pathLen) = 0

  Dim fp As Pointer = Fopen(pathBuf, "rb")
  If IsNull(fp) Then
    Printf(MSG_LEX_OPEN_FAIL, pathBuf)
    Return -1
  End If

  fseek(fp, 0, 2)
  Dim fsizeLong As Long = ftell(fp)
  fseek(fp, 0, 0)
  Dim fsize As Integer = fsizeLong

  ' Read into temp arena buffer and copy into g_srcPool
  Dim tmpBuf As Pointer = ArenaAllocBytes(ByRef arena, fsize + 1)
  If IsNull(tmpBuf) Then
    fclose(fp)
    Printf(MSG_LEX_OOM)
    Return -1
  End If
  fread(tmpBuf, 1, fsizeLong, fp)
  fclose(fp)

  Dim fileBase As Integer = g_srcPoolLen
  Dim ci As Integer = 0
  While ci < fsize Do
    g_srcPool(g_srcPoolLen) = tmpBuf(ci)
    g_srcPoolLen = g_srcPoolLen + 1
    ci = ci + 1
  End While
  g_srcPool(g_srcPoolLen) = 10
  g_srcPoolLen = g_srcPoolLen + 1
  Dim fileEnd As Integer = fileBase + fsize

  Dim pos As Integer = fileBase
  Dim line As Integer = 1

  While pos < fileEnd Do
    Dim c As Integer = g_srcPool(pos)
    If (c = 32) Or (c = 9) Or (c = 13) Then
      pos = pos + 1
    Else
      If c = 10 Then
        LxEmitTok(TokenKind.TkNewline, pos, 1, 0, line)
        pos = pos + 1
        line = line + 1
      Else
        If c = 39 Then
          While (pos < fileEnd) And (g_srcPool(pos) <> 10) Do
            pos = pos + 1
          End While
        Else
          If c = 34 Then
            pos = pos + 1
            Dim sStart As Integer = pos
            While (pos < fileEnd) And (g_srcPool(pos) <> 34) Do
              Dim sc As Integer = g_srcPool(pos)
              If (sc = 92) And ((pos + 1) < fileEnd) Then
                pos = pos + 2
              Else
                pos = pos + 1
              End If
            End While
            Dim sLen As Integer = pos - sStart
            If pos < fileEnd Then
              pos = pos + 1
            End If
            LxEmitTok(TokenKind.TkStrLit, sStart, sLen, 0, line)
          Else
            If LxIsAlpha(c) Then
              Dim idStart As Integer = pos
              While (pos < fileEnd) And LxIsAlnum(g_srcPool(pos)) Do
                pos = pos + 1
              End While
              Dim idLen As Integer = pos - idStart
              Dim kw As Integer = LxClassifyKeyword(idStart, idLen)
              If kw = TokenKind.TkInclude Then
                ' Skip whitespace then read "path.vb" and recursively include
                While (pos < fileEnd) And ((g_srcPool(pos) = 32) Or (g_srcPool(pos) = 9)) Do
                  pos = pos + 1
                End While
                If (pos < fileEnd) And (g_srcPool(pos) = 34) Then
                  pos = pos + 1
                  Dim incStart As Integer = pos
                  While (pos < fileEnd) And (g_srcPool(pos) <> 34) Do
                    pos = pos + 1
                  End While
                  Dim incLen As Integer = pos - incStart
                  If pos < fileEnd Then
                    pos = pos + 1
                  End If
                  Dim resLen As Integer = 0
                  Dim resPos As Integer = LxResolveIncludePath(pathPos, pathLen, incStart, incLen, ByRef resLen)
                  If resPos < 0 Then
                    Return -1
                  End If
                  Dim incRc As Integer = LexerLoadFile(ByRef arena, resPos, resLen)
                  If incRc < 0 Then
                    Return -1
                  End If
                  LxEmitTok(TokenKind.TkNewline, pos, 1, 0, line)
                End If
              Else
                LxEmitTok(kw, idStart, idLen, 0, line)
              End If
            Else
              If LxIsDigit(c) Then
                Dim nStart As Integer = pos
                Dim acc As Integer = 0
                Dim nextC As Integer = 0
                If (pos + 1) < fileEnd Then
                  nextC = g_srcPool(pos + 1)
                End If
                If (c = 48) And ((nextC = 120) Or (nextC = 88)) Then
                  pos = pos + 2
                  While (pos < fileEnd) And LxIsHexDigit(g_srcPool(pos)) Do
                    acc = acc * 16 + LxHexVal(g_srcPool(pos))
                    pos = pos + 1
                  End While
                  LxEmitTok(TokenKind.TkIntLit, nStart, pos - nStart, acc, line)
                Else
                  While (pos < fileEnd) And LxIsDigit(g_srcPool(pos)) Do
                    Dim d As Integer = g_srcPool(pos)
                    acc = acc * 10 + (d - 48)
                    pos = pos + 1
                  End While
                  Dim isFloat As Boolean = False
                  If (pos + 1 < fileEnd) And (g_srcPool(pos) = 46) And LxIsDigit(g_srcPool(pos + 1)) Then
                    isFloat = True
                    pos = pos + 1
                    While (pos < fileEnd) And LxIsDigit(g_srcPool(pos)) Do
                      pos = pos + 1
                    End While
                  End If
                  If isFloat Then
                    LxEmitTok(TokenKind.TkFloatLit, nStart, pos - nStart, 0, line)
                  Else
                    LxEmitTok(TokenKind.TkIntLit, nStart, pos - nStart, acc, line)
                  End If
                End If
              Else
                Dim nc1 As Integer = 0
                Dim nc2 As Integer = 0
                If (pos + 1) < fileEnd Then
                  nc1 = g_srcPool(pos + 1)
                End If
                If (pos + 2) < fileEnd Then
                  nc2 = g_srcPool(pos + 2)
                End If
                If (c = 46) And (nc1 = 46) And (nc2 = 46) Then
                  LxEmitTok(TokenKind.TkEllipsis, pos, 3, 0, line)
                  pos = pos + 3
                Else
                  If (c = 60) And (nc1 = 62) Then
                    LxEmitTok(TokenKind.TkNeq, pos, 2, 0, line)
                    pos = pos + 2
                  Else
                    If (c = 60) And (nc1 = 61) Then
                      LxEmitTok(TokenKind.TkLe, pos, 2, 0, line)
                      pos = pos + 2
                    Else
                      If (c = 62) And (nc1 = 61) Then
                        LxEmitTok(TokenKind.TkGe, pos, 2, 0, line)
                        pos = pos + 2
                      Else
                        Select Case c
                          Case 40
                            LxEmitTok(TokenKind.TkLParen, pos, 1, 0, line)
                          Case 41
                            LxEmitTok(TokenKind.TkRParen, pos, 1, 0, line)
                          Case 44
                            LxEmitTok(TokenKind.TkComma, pos, 1, 0, line)
                          Case 46
                            LxEmitTok(TokenKind.TkDot, pos, 1, 0, line)
                          Case 61
                            LxEmitTok(TokenKind.TkEq, pos, 1, 0, line)
                          Case 60
                            LxEmitTok(TokenKind.TkLt, pos, 1, 0, line)
                          Case 62
                            LxEmitTok(TokenKind.TkGt, pos, 1, 0, line)
                          Case 43
                            LxEmitTok(TokenKind.TkPlus, pos, 1, 0, line)
                          Case 45
                            LxEmitTok(TokenKind.TkMinus, pos, 1, 0, line)
                          Case 42
                            LxEmitTok(TokenKind.TkStar, pos, 1, 0, line)
                          Case 47
                            LxEmitTok(TokenKind.TkSlash, pos, 1, 0, line)
                          Case 38
                            LxEmitTok(TokenKind.TkAmp, pos, 1, 0, line)
                          Case Else
                            Printf(MSG_LEX_UNEXPECTED_CHAR, c, line)
                            Return -1
                        End Select
                        pos = pos + 1
                      End If
                    End If
                  End If
                End If
              End If
            End If
          End If
        End If
      End If
    End If
  End While

  LxEmitTok(TokenKind.TkNewline, fileEnd, 1, 0, line)
  lx_loaded(mySlot).inProgress = 0
  lx_curModId = savedModId
  Return 0
End Function

Public Function LexerLoadEntry(ByRef arena As Arena, entryPath As Pointer) As Integer
  g_srcPoolCap = 1048576
  g_srcPoolLen = 0
  g_srcPool = ArenaAllocBytes(ByRef arena, g_srcPoolCap)
  g_tokCap = 131072
  g_tokCount = 0
  g_tokens = ArenaAlloc(ByRef arena, Token, g_tokCap)
  lx_loadedCount = 0
  lx_curModId = 0

  Dim rawPos As Integer = LxCopyCStringToPool(entryPath)
  Dim rawLen As Integer = g_srcPoolLen - rawPos - 1
  Dim pLen As Integer = 0
  Dim pPos As Integer = LxNormalizePath(rawPos, rawLen, ByRef pLen)
  Dim rc As Integer = LexerLoadFile(ByRef arena, pPos, pLen)
  If rc < 0 Then
    Return -1
  End If
  LxEmitTok(TokenKind.TkEof, g_srcPoolLen, 0, 0, 0)
  Return g_tokCount
End Function
