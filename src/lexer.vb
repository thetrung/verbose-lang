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
Private Static lx_loadedCount = 0, lx_curModId = 0, lx_incDirCount = 0, lx_interpDepth = 0 As Integer

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
  Match With
    Case (c >= 65) And (c <= 90) Then Return True
    Case (c >= 97) And (c <= 122) Then Return True
    Case c = 95 Then Return True
    Case _ Then Return False
  End Match
End Function

Private Function LxIsDigit(c As Integer) As Boolean
  Return (c >= 48) And (c <= 57)
End Function

Private Function LxIsHexDigit(c As Integer) As Boolean
  Match With
    Case LxIsDigit(c) Then Return True
    Case (c >= 65) And (c <= 70) Then Return True
    Case (c >= 97) And (c <= 102) Then Return True
    Case _ Then Return False
  End Match
End Function

Private Function LxHexVal(c As Integer) As Integer
  Match With
    Case (c >= 48) And (c <= 57) Then Return c - 48
    Case (c >= 65) And (c <= 70) Then Return c - 55
    Case _ Then Return c - 87
  End Match
End Function

Private Function LxIsAlnum(c As Integer) As Boolean
  Return LxIsAlpha(c) Or LxIsDigit(c)
End Function

Public Function SliceEqualsStr(pos, slen As Integer, lit As Pointer, litLen As Integer) As Boolean
  If slen <> litLen Then
    Return False
  End If
  For i = 0 To slen - 1 Do
    Dim a = g_srcPool(pos + i), b = lit(i) As Integer
    If a <> b Then
      Return False
    End If
  End For
  Return True
End Function

Public Function SlicesEqual(p1, l1, p2, l2 As Integer) As Boolean
  If l1 <> l2 Then
    Return False
  End If
  For i = 0 To l1 - 1 Do
    Dim a = g_srcPool(p1 + i), b = g_srcPool(p2 + i) As Integer
    If a <> b Then
      Return False
    End If
  End For
  Return True
End Function

Private Function LxClassifyKeyword(pos, slen As Integer) As Integer
  Match With
    Case SliceEqualsStr(pos, slen, "Include", 7) Then Return TkInclude
    Case SliceEqualsStr(pos, slen, "Public", 6) Then Return TkPublic
    Case SliceEqualsStr(pos, slen, "Private", 7) Then Return TkPrivate
    Case SliceEqualsStr(pos, slen, "Declare", 7) Then Return TkDeclare
    Case SliceEqualsStr(pos, slen, "Structure", 9) Then Return TkStructure
    Case SliceEqualsStr(pos, slen, "Enum", 4) Then Return TkEnum
    Case SliceEqualsStr(pos, slen, "Static", 6) Then Return TkStatic
    Case SliceEqualsStr(pos, slen, "Function", 8) Then Return TkFunction
    Case SliceEqualsStr(pos, slen, "Sub", 3) Then Return TkSub
    Case SliceEqualsStr(pos, slen, "End", 3) Then Return TkEnd
    Case SliceEqualsStr(pos, slen, "Dim", 3) Then Return TkDim
    Case SliceEqualsStr(pos, slen, "As", 2) Then Return TkAs
    Case SliceEqualsStr(pos, slen, "ByRef", 5) Then Return TkByRef
    Case SliceEqualsStr(pos, slen, "With", 4) Then Return TkWith
    Case SliceEqualsStr(pos, slen, "Return", 6) Then Return TkReturn
    Case SliceEqualsStr(pos, slen, "Test", 4) Then Return TkTest
    Case SliceEqualsStr(pos, slen, "Assert", 6) Then Return TkAssert
    Case SliceEqualsStr(pos, slen, "Volatile", 8) Then Return TkVolatile
    Case SliceEqualsStr(pos, slen, "VolatileLoad", 12) Then Return TkVolatileLoad
    Case SliceEqualsStr(pos, slen, "VolatileStore", 13) Then Return TkVolatileStore
    Case SliceEqualsStr(pos, slen, "AddressOf", 9) Then Return TkAddressOf
    Case SliceEqualsStr(pos, slen, "PtrRead", 7) Then Return TkPtrRead
    Case SliceEqualsStr(pos, slen, "PtrWrite", 8) Then Return TkPtrWrite
    Case SliceEqualsStr(pos, slen, "PtrAdd", 6) Then Return TkPtrAdd
    Case SliceEqualsStr(pos, slen, "Label", 5) Then Return TkLabel
    Case SliceEqualsStr(pos, slen, "GoTo", 4) Then Return TkGoTo
    Case SliceEqualsStr(pos, slen, "Async", 5) Then Return TkAsync
    Case SliceEqualsStr(pos, slen, "Await", 5) Then Return TkAwait
    Case SliceEqualsStr(pos, slen, "Task", 4) Then Return TkTask
    Case SliceEqualsStr(pos, slen, "Integer", 7) Then Return TkInteger
    Case SliceEqualsStr(pos, slen, "Byte", 4) Then Return TkByte
    Case SliceEqualsStr(pos, slen, "Short", 5) Then Return TkShort
    Case SliceEqualsStr(pos, slen, "Long", 4) Then Return TkLong
    Case SliceEqualsStr(pos, slen, "Boolean", 7) Then Return TkBoolean
    Case SliceEqualsStr(pos, slen, "Pointer", 7) Then Return TkPointer
    Case SliceEqualsStr(pos, slen, "String", 6) Then Return TkString
    Case SliceEqualsStr(pos, slen, "Arena", 5) Then Return TkArena
    Case SliceEqualsStr(pos, slen, "Nothing", 7) Then Return TkNothing
    Case SliceEqualsStr(pos, slen, "Single", 6) Then Return TkSingle
    Case SliceEqualsStr(pos, slen, "Double", 6) Then Return TkDouble
    Case SliceEqualsStr(pos, slen, "True", 4) Then Return TkTrue
    Case SliceEqualsStr(pos, slen, "False", 5) Then Return TkFalse
    Case SliceEqualsStr(pos, slen, "SizeOf", 6) Then Return TkSizeOf
    Case SliceEqualsStr(pos, slen, "ArenaAlloc", 10) Then Return TkArenaAlloc
    Case SliceEqualsStr(pos, slen, "HeapAlloc", 9) Then Return TkHeapAlloc
    Case SliceEqualsStr(pos, slen, "HeapRealloc", 11) Then Return TkHeapRealloc
    Case SliceEqualsStr(pos, slen, "If", 2) Then Return TkIf
    Case SliceEqualsStr(pos, slen, "Then", 4) Then Return TkThen
    Case SliceEqualsStr(pos, slen, "Else", 4) Then Return TkElse
    Case SliceEqualsStr(pos, slen, "While", 5) Then Return TkWhile
    Case SliceEqualsStr(pos, slen, "Do", 2) Then Return TkDo
    Case SliceEqualsStr(pos, slen, "For", 3) Then Return TkFor
    Case SliceEqualsStr(pos, slen, "To", 2) Then Return TkTo
    Case SliceEqualsStr(pos, slen, "Select", 6) Then Return TkSelect
    Case SliceEqualsStr(pos, slen, "Case", 4) Then Return TkCase
    Case SliceEqualsStr(pos, slen, "Match", 5) Then Return TkMatch
    Case SliceEqualsStr(pos, slen, "When", 4) Then Return TkWhen
    Case SliceEqualsStr(pos, slen, "Break", 5) Then Return TkBreak
    Case SliceEqualsStr(pos, slen, "Continue", 8) Then Return TkContinue
    Case SliceEqualsStr(pos, slen, "Operator", 8) Then Return TkOperator
    Case SliceEqualsStr(pos, slen, "Of", 2) Then Return TkOf
    Case SliceEqualsStr(pos, slen, "Union", 5) Then Return TkUnion
    Case SliceEqualsStr(pos, slen, "Defer", 5) Then Return TkDefer
    Case SliceEqualsStr(pos, slen, "FuncPtr", 7) Then Return TkFuncPtr
    Case SliceEqualsStr(pos, slen, "SubPtr", 6) Then Return TkSubPtr
    Case SliceEqualsStr(pos, slen, "And", 3) Then Return TkAnd
    Case SliceEqualsStr(pos, slen, "Or", 2) Then Return TkOr
    Case SliceEqualsStr(pos, slen, "Not", 3) Then Return TkNot
    Case SliceEqualsStr(pos, slen, "Mod", 3) Then Return TkMod
    Case SliceEqualsStr(pos, slen, "Xor", 3) Then Return TkXor
    Case SliceEqualsStr(pos, slen, "Shl", 3) Then Return TkShl
    Case SliceEqualsStr(pos, slen, "Shr", 3) Then Return TkShr
    Case _ Then Return TkIdent
  End Match
End Function

Private Sub LxEmitTok(kind, pos, slen As Integer, ival As Long, line As Integer)
  If g_tokCount < g_tokCap Then
    g_tokens(g_tokCount) = Token With (
      kind = kind,
      pos = pos,
      len = slen,
      intVal = Integer(ival),
      longVal = ival,
      line = line,
      modId = lx_curModId
    )
    g_tokCount = g_tokCount + 1
  End If
End Sub

Private Function LxLexInterpChunk(ByRef pos As Integer, fileEnd As Integer, line As Integer) As Integer
  Dim chunkStart As Integer = g_srcPoolLen
  While pos < fileEnd Do
    Dim sc As Integer = g_srcPool(pos)
    If sc = 34 Then
      Dim cLen As Integer = g_srcPoolLen - chunkStart
      If cLen > 0 Then
        LxEmitTok(TkStrLit, chunkStart, cLen, 0, line)
      End If
      LxEmitTok(TkInterpEnd, pos, 1, 0, line)
      pos = pos + 1
      Return 0
    End If
    If (sc = 92) And ((pos + 1) < fileEnd) Then
      If g_srcPoolLen < (g_srcPoolCap - 2) Then
        g_srcPool(g_srcPoolLen) = sc
        g_srcPool(g_srcPoolLen + 1) = g_srcPool(pos + 1)
        g_srcPoolLen = g_srcPoolLen + 2
      End If
      pos = pos + 2
    Else
      If sc = 123 Then
        If ((pos + 1) < fileEnd) And (g_srcPool(pos + 1) = 123) Then
          If g_srcPoolLen < (g_srcPoolCap - 1) Then
            g_srcPool(g_srcPoolLen) = 123
            g_srcPoolLen = g_srcPoolLen + 1
          End If
          pos = pos + 2
        Else
          Dim cLen As Integer = g_srcPoolLen - chunkStart
          If cLen > 0 Then
            LxEmitTok(TkStrLit, chunkStart, cLen, 0, line)
          End If
          LxEmitTok(TkInterpExprStart, pos, 1, 0, line)
          pos = pos + 1
          lx_interpDepth = lx_interpDepth + 1
          Return 0
        End If
      Else
        If sc = 125 Then
          If ((pos + 1) < fileEnd) And (g_srcPool(pos + 1) = 125) Then
            If g_srcPoolLen < (g_srcPoolCap - 1) Then
              g_srcPool(g_srcPoolLen) = 125
              g_srcPoolLen = g_srcPoolLen + 1
            End If
            pos = pos + 2
          Else
            If g_srcPoolLen < (g_srcPoolCap - 1) Then
              g_srcPool(g_srcPoolLen) = 125
              g_srcPoolLen = g_srcPoolLen + 1
            End If
            pos = pos + 1
          End If
        Else
          If g_srcPoolLen < (g_srcPoolCap - 1) Then
            g_srcPool(g_srcPoolLen) = sc
            g_srcPoolLen = g_srcPoolLen + 1
          End If
          pos = pos + 1
        End If
      End If
    End If
  End While
  Printf(MSG_LEX_UNTERM_INTERP, line)
  Return -1
End Function

Private Function LxFindLoaded(pPos, pLen As Integer) As Integer
  For i = 0 To lx_loadedCount - 1 Do
    If SlicesEqual(lx_loaded(i).pathPos, lx_loaded(i).pathLen, pPos, pLen) Then
      Return i
    End If
  End For
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
  Dim segCnt = 0 As Integer, isAbs = False As Boolean, isDrive = False As Boolean
  If rawLen > 0 Then
    Dim c0 As Integer = g_srcPool(rawPos)
    If (c0 = 47) Or (c0 = 92) Then
      isAbs = True
    End If
  End If
  If (rawLen >= 2) And (g_srcPool(rawPos + 1) = 58) Then
    isDrive = True
  End If
  Dim i As Integer = 0
  While i < rawLen Do
    While (i < rawLen) And ((g_srcPool(rawPos + i) = 47) Or (g_srcPool(rawPos + i) = 92)) Do
      i = i + 1
    End While
    Dim sStart As Integer = i
    While (i < rawLen) And (g_srcPool(rawPos + i) <> 47) And (g_srcPool(rawPos + i) <> 92) Do
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
            Dim topIsDrive As Boolean = isDrive And (segCnt = 1)
            If (Not topIsDD) And (Not topIsDrive) Then
              canPop = True
            End If
          End If
          If canPop Then
            segCnt = segCnt - 1
          Else
            If (Not isAbs) And (Not isDrive) Then
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
  For k = 0 To segCnt - 1 Do
    If k > 0 Then
      g_srcPool(g_srcPoolLen) = 47
      g_srcPoolLen = g_srcPoolLen + 1
    End If
    For j = 0 To segLen(k) - 1 Do
      g_srcPool(g_srcPoolLen) = g_srcPool(segPos(k) + j)
      g_srcPoolLen = g_srcPoolLen + 1
    End For
  End For
  outLen = g_srcPoolLen - startPos
  g_srcPool(g_srcPoolLen) = 0
  g_srcPoolLen = g_srcPoolLen + 1
  Return startPos
End Function

' Resolve relPos..relPos+relLen relative to curPathPos..curPathPos+curPathLen into g_srcPool
Private Function LxResolveRelPath(curPos, curLen, relPos, relLen As Integer, ByRef outLen As Integer) As Integer
  Dim startPos As Integer = g_srcPoolLen
  Dim isRelAbs As Boolean = False
  If relLen > 0 Then
    Dim firstRel As Integer = g_srcPool(relPos)
    If (firstRel = 47) Or (firstRel = 92) Then
      isRelAbs = True
    End If
  End If
  If (relLen >= 2) And (g_srcPool(relPos + 1) = 58) Then
    isRelAbs = True
  End If
  If Not isRelAbs Then
    ' Find last '/' or '\' in curPath
    Dim lastSlash As Integer = -1
    For i = 0 To curLen - 1 Do
      Dim ch As Integer = g_srcPool(curPos + i)
      If (ch = 47) Or (ch = 92) Then
        lastSlash = i
      End If
    End For
    If lastSlash >= 0 Then
      For j = 0 To lastSlash Do
        g_srcPool(g_srcPoolLen) = g_srcPool(curPos + j)
        g_srcPoolLen = g_srcPoolLen + 1
      End For
    End If
  End If
  For k = 0 To relLen - 1 Do
    g_srcPool(g_srcPoolLen) = g_srcPool(relPos + k)
    g_srcPoolLen = g_srcPoolLen + 1
  End For
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
  For d = 0 To lx_incDirCount - 1 Do
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
    If g_srcPoolLen > startPos Then
      Dim lastCh As Integer = g_srcPool(g_srcPoolLen - 1)
      If (lastCh <> 47) And (lastCh <> 92) Then
        g_srcPool(g_srcPoolLen) = 47
        g_srcPoolLen = g_srcPoolLen + 1
      End If
    End If
    For k = 0 To incLen - 1 Do
      g_srcPool(g_srcPoolLen) = g_srcPool(incPos + k)
      g_srcPoolLen = g_srcPoolLen + 1
    End For
    Dim rawLen As Integer = g_srcPoolLen - startPos
    g_srcPool(g_srcPoolLen) = 0
    g_srcPoolLen = g_srcPoolLen + 1
    Dim normLen As Integer = 0
    Dim normPos As Integer = LxNormalizePath(startPos, rawLen, ByRef normLen)
    If LxFileExists(normPos, normLen) Then
      outLen = normLen
      Return normPos
    End If
  End For

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
  For pi = 0 To pathLen - 1 Do
    pathBuf(pi) = g_srcPool(pathPos + pi)
  End For
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
  For ci = 0 To fsize - 1 Do
    g_srcPool(g_srcPoolLen) = tmpBuf(ci)
    g_srcPoolLen = g_srcPoolLen + 1
  End For
  g_srcPool(g_srcPoolLen) = 10
  g_srcPoolLen = g_srcPoolLen + 1
  Dim fileEnd As Integer = fileBase + fsize

  Dim pos As Integer = fileBase
  Dim line As Integer = 1

  While pos < fileEnd Do
    Dim c As Integer = g_srcPool(pos)
    Match With
      Case (c = 32) Or (c = 9) Or (c = 13) Then
        pos = pos + 1
      Case c = 10 Then
        LxEmitTok(TkNewline, pos, 1, 0, line)
        pos = pos + 1
        line = line + 1
      Case c = 39 Then
        While (pos < fileEnd) And (g_srcPool(pos) <> 10) Do
          pos = pos + 1
        End While
      Case (c = 36) And ((pos + 1) < fileEnd) And (g_srcPool(pos + 1) = 34) Then
        LxEmitTok(TkInterpStart, pos, 2, 0, line)
        pos = pos + 2
        If LxLexInterpChunk(ByRef pos, fileEnd, line) < 0 Then
          Return -1
        End If
      Case (c = 125) And (lx_interpDepth > 0) Then
        LxEmitTok(TkInterpExprEnd, pos, 1, 0, line)
        pos = pos + 1
        lx_interpDepth = lx_interpDepth - 1
        If LxLexInterpChunk(ByRef pos, fileEnd, line) < 0 Then
          Return -1
        End If
      Case c = 34 Then
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
        LxEmitTok(TkStrLit, sStart, sLen, 0, line)
      Case LxIsAlpha(c) Then
        Dim idStart As Integer = pos
        While (pos < fileEnd) And LxIsAlnum(g_srcPool(pos)) Do
          pos = pos + 1
        End While
        Dim idLen As Integer = pos - idStart
        Dim kw As Integer = LxClassifyKeyword(idStart, idLen)
        If kw = TkInclude Then
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
            If LexerLoadFile(ByRef arena, resPos, resLen) < 0 Then
              Return -1
            End If
            LxEmitTok(TkNewline, pos, 1, 0, line)
          End If
        Else
          LxEmitTok(kw, idStart, idLen, 0, line)
        End If
      Case LxIsDigit(c) Then
        Dim nStart As Integer = pos
        Dim acc As Long = 0
        Dim nextC As Integer = 0
        If (pos + 1) < fileEnd Then
          nextC = g_srcPool(pos + 1)
        End If
        If (c = 48) And ((nextC = 120) Or (nextC = 88)) Then
          pos = pos + 2
          While (pos < fileEnd) And LxIsHexDigit(g_srcPool(pos)) Do
            acc = acc * 16 + Long(LxHexVal(g_srcPool(pos)))
            pos = pos + 1
          End While
          LxEmitTok(TkIntLit, nStart, pos - nStart, acc, line)
        Else
          While (pos < fileEnd) And LxIsDigit(g_srcPool(pos)) Do
            Dim d As Integer = g_srcPool(pos)
            acc = acc * 10 + Long(d - 48)
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
          If (pos < fileEnd) And ((g_srcPool(pos) = 101) Or (g_srcPool(pos) = 69)) Then
            Dim ePos As Integer = pos + 1
            If (ePos < fileEnd) And ((g_srcPool(ePos) = 43) Or (g_srcPool(ePos) = 45)) Then
              ePos = ePos + 1
            End If
            If (ePos < fileEnd) And LxIsDigit(g_srcPool(ePos)) Then
              isFloat = True
              pos = ePos
              While (pos < fileEnd) And LxIsDigit(g_srcPool(pos)) Do
                pos = pos + 1
              End While
            End If
          End If
          If isFloat Then
            LxEmitTok(TkFloatLit, nStart, pos - nStart, 0, line)
          Else
            LxEmitTok(TkIntLit, nStart, pos - nStart, acc, line)
          End If
        End If
      Case _ Then
        Dim nc1 = 0, nc2 = 0 As Integer
        If (pos + 1) < fileEnd Then
          nc1 = g_srcPool(pos + 1)
        End If
        If (pos + 2) < fileEnd Then
          nc2 = g_srcPool(pos + 2)
        End If
        Match With
          Case (c = 46) And (nc1 = 46) And (nc2 = 46) Then
            LxEmitTok(TkEllipsis, pos, 3, 0, line)
            pos = pos + 3
          Case (c = 60) And (nc1 = 62) Then
            LxEmitTok(TkNeq, pos, 2, 0, line)
            pos = pos + 2
          Case (c = 60) And (nc1 = 61) Then
            LxEmitTok(TkLe, pos, 2, 0, line)
            pos = pos + 2
          Case (c = 62) And (nc1 = 61) Then
            LxEmitTok(TkGe, pos, 2, 0, line)
            pos = pos + 2
          Case _ Then
            Match c With
              Case 40 Then LxEmitTok(TkLParen, pos, 1, 0, line)
              Case 41 Then LxEmitTok(TkRParen, pos, 1, 0, line)
              Case 44 Then LxEmitTok(TkComma, pos, 1, 0, line)
              Case 46 Then LxEmitTok(TkDot, pos, 1, 0, line)
              Case 61 Then LxEmitTok(TkEq, pos, 1, 0, line)
              Case 60 Then LxEmitTok(TkLt, pos, 1, 0, line)
              Case 62 Then LxEmitTok(TkGt, pos, 1, 0, line)
              Case 43 Then LxEmitTok(TkPlus, pos, 1, 0, line)
              Case 45 Then LxEmitTok(TkMinus, pos, 1, 0, line)
              Case 42 Then LxEmitTok(TkStar, pos, 1, 0, line)
              Case 47 Then LxEmitTok(TkSlash, pos, 1, 0, line)
              Case 38 Then LxEmitTok(TkAmp, pos, 1, 0, line)
              Case _ Then
                Printf(MSG_LEX_UNEXPECTED_CHAR, c, line)
                Return -1
            End Match
            pos = pos + 1
        End Match
    End Match
  End While

  LxEmitTok(TkNewline, fileEnd, 1, 0, line)
  lx_loaded(mySlot).inProgress = 0
  lx_curModId = savedModId
  Return 0
End Function

Public Function LexerLoadEntry(ByRef arena As Arena, entryPath As Pointer) As Integer
  g_srcPoolCap = 2097152
  g_srcPoolLen = 0
  g_srcPool = ArenaAllocBytes(ByRef arena, g_srcPoolCap)
  g_tokCap = 262144
  g_tokCount = 0
  g_tokens = ArenaAlloc(ByRef arena, Token, g_tokCap)
  lx_loadedCount = 0
  lx_curModId = 0
  lx_interpDepth = 0

  Dim rawPos As Integer = LxCopyCStringToPool(entryPath)
  Dim rawLen As Integer = g_srcPoolLen - rawPos - 1
  Dim pLen As Integer = 0
  Dim pPos As Integer = LxNormalizePath(rawPos, rawLen, ByRef pLen)
  Dim rc As Integer = LexerLoadFile(ByRef arena, pPos, pLen)
  If rc < 0 Then
    Return -1
  End If
  LxEmitTok(TkEof, g_srcPoolLen, 0, 0, 0)
  Return g_tokCount
End Function
