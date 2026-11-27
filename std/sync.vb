' std/sync.vb — Pure-VerBose synchronization & concurrency primitives.
' Core lock-free & atomic primitives (AtomicInt, AtomicLong, SpinLock, RwSpinLock, Once,
' WaitGroup, SpinBarrier) are 100% pure LLVM atomics (zero libc, bare-metal arm-m safe).
' Hosted OS-blocking primitives (Mutex, CondVar) use native POSIX/winpthreads FFI
' (auto-linking -lpthread on Linux/Windows when called).

Declare Function pthread_mutex_init(mtx As Pointer, attr As Pointer) As Integer
Declare Function pthread_mutex_destroy(mtx As Pointer) As Integer
Declare Function pthread_mutex_lock(mtx As Pointer) As Integer
Declare Function pthread_mutex_trylock(mtx As Pointer) As Integer
Declare Function pthread_mutex_unlock(mtx As Pointer) As Integer
Declare Function pthread_cond_init(cond As Pointer, attr As Pointer) As Integer
Declare Function pthread_cond_destroy(cond As Pointer) As Integer
Declare Function pthread_cond_wait(cond As Pointer, mtx As Pointer) As Integer
Declare Function pthread_cond_signal(cond As Pointer) As Integer
Declare Function pthread_cond_broadcast(cond As Pointer) As Integer

' =============================================================================
' Lock-Free Atomic Wrappers (Bare-Metal Safe)
' =============================================================================

Public Structure AtomicInt
  Dim val As Integer
End Structure

Public Structure AtomicLong
  Dim val As Long
End Structure

Public Function AtomicIntNew(initial As Integer) As AtomicInt
  Return AtomicInt With ( val = initial )
End Function

Public Function AtomicIntLoad(ByRef a As AtomicInt) As Integer
  Return AtomicLoad(AddressOf(a.val), Integer)
End Function

Public Sub AtomicIntStore(ByRef a As AtomicInt, v As Integer)
  AtomicStore(AddressOf(a.val), Integer, v)
End Sub

Public Function AtomicIntAdd(ByRef a As AtomicInt, delta As Integer) As Integer
  Return AtomicAdd(AddressOf(a.val), Integer, delta)
End Function

Public Function AtomicIntSub(ByRef a As AtomicInt, delta As Integer) As Integer
  Return AtomicSub(AddressOf(a.val), Integer, delta)
End Function

Public Function AtomicIntExchange(ByRef a As AtomicInt, desired As Integer) As Integer
  Return AtomicExchange(AddressOf(a.val), Integer, desired)
End Function

Public Function AtomicIntCas(ByRef a As AtomicInt, expected, desired As Integer) As Boolean
  Return AtomicCas(AddressOf(a.val), Integer, expected, desired)
End Function

Public Function AtomicLongNew(initial As Long) As AtomicLong
  Return AtomicLong With ( val = initial )
End Function

Public Function AtomicLongLoad(ByRef a As AtomicLong) As Long
  Return AtomicLoad(AddressOf(a.val), Long)
End Function

Public Sub AtomicLongStore(ByRef a As AtomicLong, v As Long)
  AtomicStore(AddressOf(a.val), Long, v)
End Sub

Public Function AtomicLongAdd(ByRef a As AtomicLong, delta As Long) As Long
  Return AtomicAdd(AddressOf(a.val), Long, delta)
End Function

Public Function AtomicLongSub(ByRef a As AtomicLong, delta As Long) As Long
  Return AtomicSub(AddressOf(a.val), Long, delta)
End Function

Public Function AtomicLongExchange(ByRef a As AtomicLong, desired As Long) As Long
  Return AtomicExchange(AddressOf(a.val), Long, desired)
End Function

Public Function AtomicLongCas(ByRef a As AtomicLong, expected, desired As Long) As Boolean
  Return AtomicCas(AddressOf(a.val), Long, expected, desired)
End Function

' =============================================================================
' SpinLock & Reader-Writer SpinLock (Bare-Metal Safe)
' =============================================================================

Public Structure SpinLock
  Dim state As Integer
End Structure

Public Function SpinLockNew() As SpinLock
  Return SpinLock With ( state = 0 )
End Function

Public Function SpinLockTryAcquire(ByRef lk As SpinLock) As Boolean
  Return AtomicCas(AddressOf(lk.state), Integer, 0, 1)
End Function

Public Sub SpinLockAcquire(ByRef lk As SpinLock)
  While Not AtomicCas(AddressOf(lk.state), Integer, 0, 1) Do
    While AtomicLoad(AddressOf(lk.state), Integer) <> 0 Do
    End While
  End While
End Sub

Public Sub SpinLockRelease(ByRef lk As SpinLock)
  AtomicStore(AddressOf(lk.state), Integer, 0)
End Sub

' state = 0 unlocked, >0 active reader count, -1 active writer
Public Structure RwSpinLock
  Dim state As Integer
End Structure

Public Function RwSpinNew() As RwSpinLock
  Return RwSpinLock With ( state = 0 )
End Function

Public Sub RwSpinReadLock(ByRef rw As RwSpinLock)
  Dim acquired As Boolean = False
  While Not acquired Do
    Dim cur As Integer = AtomicLoad(AddressOf(rw.state), Integer)
    If cur >= 0 Then
      If AtomicCas(AddressOf(rw.state), Integer, cur, cur + 1) Then
        acquired = True
      End If
    End If
  End While
End Sub

Public Sub RwSpinReadUnlock(ByRef rw As RwSpinLock)
  AtomicSub(AddressOf(rw.state), Integer, 1)
End Sub

Public Sub RwSpinWriteLock(ByRef rw As RwSpinLock)
  While Not AtomicCas(AddressOf(rw.state), Integer, 0, -1) Do
    While AtomicLoad(AddressOf(rw.state), Integer) <> 0 Do
    End While
  End While
End Sub

Public Sub RwSpinWriteUnlock(ByRef rw As RwSpinLock)
  AtomicStore(AddressOf(rw.state), Integer, 0)
End Sub

' =============================================================================
' Once, WaitGroup & Reusable SpinBarrier (Bare-Metal Safe)
' =============================================================================

' state: 0 = uninitialized, 1 = running, 2 = completed
Public Structure Once
  Dim state As Integer
End Structure

Public Function OnceNew() As Once
  Return Once With ( state = 0 )
End Function

Public Sub OnceCall(ByRef o As Once, fn As SubPtr())
  If AtomicLoad(AddressOf(o.state), Integer) = 2 Then
    Return
  End If
  If AtomicCas(AddressOf(o.state), Integer, 0, 1) Then
    If Not IsNull(fn) Then
      fn()
    End If
    AtomicStore(AddressOf(o.state), Integer, 2)
  Else
    While AtomicLoad(AddressOf(o.state), Integer) <> 2 Do
    End While
  End If
End Sub

Public Structure WaitGroup
  Dim count As Integer
End Structure

Public Function WgNew() As WaitGroup
  Return WaitGroup With ( count = 0 )
End Function

Public Sub WgAdd(ByRef wg As WaitGroup, delta As Integer)
  AtomicAdd(AddressOf(wg.count), Integer, delta)
End Sub

Public Sub WgDone(ByRef wg As WaitGroup)
  AtomicSub(AddressOf(wg.count), Integer, 1)
End Sub

Public Sub WgWait(ByRef wg As WaitGroup)
  While AtomicLoad(AddressOf(wg.count), Integer) > 0 Do
  End While
End Sub

Public Structure SpinBarrier
  Dim total, waiting, gen As Integer
End Structure

Public Function BarrierNew(partyCount As Integer) As SpinBarrier
  Dim n As Integer = partyCount
  If n < 1 Then
    n = 1
  End If
  Return SpinBarrier With ( total = n, waiting = 0, gen = 0 )
End Function

' Returns True for the single leader thread that tripped the barrier generation
Public Function BarrierWait(ByRef b As SpinBarrier) As Boolean
  Dim myGen As Integer = AtomicLoad(AddressOf(b.gen), Integer)
  Dim prev As Integer = AtomicAdd(AddressOf(b.waiting), Integer, 1)
  If (prev + 1) = b.total Then
    AtomicStore(AddressOf(b.waiting), Integer, 0)
    AtomicAdd(AddressOf(b.gen), Integer, 1)
    Return True
  End If
  While AtomicLoad(AddressOf(b.gen), Integer) = myGen Do
  End While
  Return False
End Function

' =============================================================================
' Hosted OS-Blocking Mutex & CondVar (POSIX / MSYS2 winpthreads)
' =============================================================================

' 64-byte 8-byte-aligned opaque storage covers glibc x86_64/arm64, macOS arm64, and winpthreads
Public Structure Mutex
  Dim raw(8) As Long
End Structure

Public Structure CondVar
  Dim raw(8) As Long
End Structure

Public Function MutexInit(ByRef m As Mutex) As Boolean
  Return pthread_mutex_init(AddressOf(m.raw(0)), 0) = 0
End Function

Public Sub MutexDestroy(ByRef m As Mutex)
  pthread_mutex_destroy(AddressOf(m.raw(0)))
End Sub

Public Sub MutexLock(ByRef m As Mutex)
  pthread_mutex_lock(AddressOf(m.raw(0)))
End Sub

Public Function MutexTryLock(ByRef m As Mutex) As Boolean
  Return pthread_mutex_trylock(AddressOf(m.raw(0))) = 0
End Function

Public Sub MutexUnlock(ByRef m As Mutex)
  pthread_mutex_unlock(AddressOf(m.raw(0)))
End Sub

Public Function CondInit(ByRef c As CondVar) As Boolean
  Return pthread_cond_init(AddressOf(c.raw(0)), 0) = 0
End Function

Public Sub CondDestroy(ByRef c As CondVar)
  pthread_cond_destroy(AddressOf(c.raw(0)))
End Sub

Public Sub CondWait(ByRef c As CondVar, ByRef m As Mutex)
  pthread_cond_wait(AddressOf(c.raw(0)), AddressOf(m.raw(0)))
End Sub

Public Sub CondSignal(ByRef c As CondVar)
  pthread_cond_signal(AddressOf(c.raw(0)))
End Sub

Public Sub CondBroadcast(ByRef c As CondVar)
  pthread_cond_broadcast(AddressOf(c.raw(0)))
End Sub
