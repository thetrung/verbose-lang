' VerBose Test Suite: Milestone 5 — Hardware Atomics & std/sync.vb

Include "harness.vb"
Include "../std/sync.vb"

Private Static g_spin As SpinLock
Private Static g_spinCounter As Integer = 0
Private Static g_rw As RwSpinLock
Private Static g_rwVal As Integer = 0
Private Static g_once As Once
Private Static g_onceRuns As AtomicInt
Private Static g_wg As WaitGroup
Private Static g_wgSum As AtomicInt
Private Static g_bar As SpinBarrier
Private Static g_barLeaders As AtomicInt
Private Static g_barPhase1 As AtomicInt
Private Static g_barPhase2Ok As AtomicInt
Private Static g_mtx As Mutex
Private Static g_mtxCounter As Integer = 0
Private Static g_condMtx As Mutex
Private Static g_cond As CondVar
Private Static g_condReady As Integer = 0

Private Sub OnceInitTarget()
  AtomicIntAdd(ByRef g_onceRuns, 1)
End Sub

Private Async Function SpinWorker(iters As Integer) As Integer
  For i = 0 To iters - 1 Do
    SpinLockAcquire(ByRef g_spin)
    g_spinCounter = g_spinCounter + 1
    SpinLockRelease(ByRef g_spin)
  End For
  Return iters
End Function

Private Async Function OnceWorker() As Integer
  OnceCall(ByRef g_once, AddressOf(OnceInitTarget))
  Return AtomicIntLoad(ByRef g_onceRuns)
End Function

Private Async Function WgWorker(v As Integer) As Integer
  AtomicIntAdd(ByRef g_wgSum, v)
  WgDone(ByRef g_wg)
  Return v
End Function

Private Async Function BarrierWorker() As Integer
  AtomicIntAdd(ByRef g_barPhase1, 1)
  If BarrierWait(ByRef g_bar) Then
    AtomicIntAdd(ByRef g_barLeaders, 1)
  End If
  If AtomicIntLoad(ByRef g_barPhase1) = 4 Then
    AtomicIntAdd(ByRef g_barPhase2Ok, 1)
  End If
  ' Second reusable barrier generation
  If BarrierWait(ByRef g_bar) Then
    AtomicIntAdd(ByRef g_barLeaders, 1)
  End If
  Return 1
End Function

Private Async Function MutexWorker(iters As Integer) As Integer
  For i = 0 To iters - 1 Do
    MutexLock(ByRef g_mtx)
    g_mtxCounter = g_mtxCounter + 1
    MutexUnlock(ByRef g_mtx)
  End For
  Return iters
End Function

Private Async Function CondNotifier() As Integer
  MutexLock(ByRef g_condMtx)
  g_condReady = 1
  CondSignal(ByRef g_cond)
  MutexUnlock(ByRef g_condMtx)
  Return 1
End Function

Test "Hardware Atomic* builtins and AtomicInt/AtomicLong wrappers"
  Dim rawI As Integer = 10
  Assert AtomicLoad(AddressOf(rawI), Integer) = 10, "AtomicLoad Integer"
  AtomicStore(AddressOf(rawI), Integer, 25)
  Assert AtomicLoad(AddressOf(rawI), Integer) = 25, "AtomicStore Integer"
  Assert AtomicAdd(AddressOf(rawI), Integer, 5) = 25, "AtomicAdd returns old"
  Assert AtomicSub(AddressOf(rawI), Integer, 8) = 30, "AtomicSub returns old"
  Assert AtomicExchange(AddressOf(rawI), Integer, 100) = 22, "AtomicExchange returns old"
  Assert Not AtomicCas(AddressOf(rawI), Integer, 99, 200), "AtomicCas fails on mismatch"
  Assert AtomicCas(AddressOf(rawI), Integer, 100, 200), "AtomicCas succeeds on match"
  Assert AtomicLoad(AddressOf(rawI), Integer) = 200, "AtomicCas updated value"
  AtomicFence()

  Dim rawP As Pointer = 0
  AtomicStore(AddressOf(rawP), Pointer, AddressOf(rawI))
  Assert AtomicLoad(AddressOf(rawP), Pointer) = AddressOf(rawI), "AtomicLoad/Store Pointer"

  Dim ai As AtomicInt = AtomicIntNew(7)
  Assert AtomicIntAdd(ByRef ai, 3) = 7, "AtomicIntAdd"
  Assert AtomicIntSub(ByRef ai, 2) = 10, "AtomicIntSub"
  Assert AtomicIntExchange(ByRef ai, 50) = 8, "AtomicIntExchange"
  Assert AtomicIntCas(ByRef ai, 50, 77), "AtomicIntCas"
  Assert AtomicIntLoad(ByRef ai) = 77, "AtomicIntLoad"

  Dim al As AtomicLong = AtomicLongNew(10000000000)
  Assert AtomicLongAdd(ByRef al, 5000000000) = 10000000000, "AtomicLongAdd 64-bit"
  Assert AtomicLongSub(ByRef al, 2000000000) = 15000000000, "AtomicLongSub 64-bit"
  Assert AtomicLongCas(ByRef al, 13000000000, 99999999999), "AtomicLongCas 64-bit"
  Assert AtomicLongLoad(ByRef al) = 99999999999, "AtomicLongLoad 64-bit"
End Test

Test "SpinLock, RwSpinLock, Once, WaitGroup, and reusable SpinBarrier under Async concurrency"
  g_spin = SpinLockNew()
  Assert SpinLockTryAcquire(ByRef g_spin), "SpinLockTryAcquire succeeds when unlocked"
  Assert Not SpinLockTryAcquire(ByRef g_spin), "SpinLockTryAcquire fails when locked"
  SpinLockRelease(ByRef g_spin)

  g_spinCounter = 0
  Dim t0 As Task(Integer) = SpinWorker(1000)
  Dim t1 As Task(Integer) = SpinWorker(1000)
  Dim t2 As Task(Integer) = SpinWorker(1000)
  Dim t3 As Task(Integer) = SpinWorker(1000)
  Await t0
  Await t1
  Await t2
  Await t3
  Assert g_spinCounter = 4000, "SpinLock protects concurrent counter"

  g_rw = RwSpinNew()
  RwSpinWriteLock(ByRef g_rw)
  g_rwVal = 42
  RwSpinWriteUnlock(ByRef g_rw)
  RwSpinReadLock(ByRef g_rw)
  RwSpinReadLock(ByRef g_rw)
  Assert g_rwVal = 42, "RwSpinLock allows multiple concurrent readers"
  RwSpinReadUnlock(ByRef g_rw)
  RwSpinReadUnlock(ByRef g_rw)

  g_once = OnceNew()
  g_onceRuns = AtomicIntNew(0)
  Dim o0 As Task(Integer) = OnceWorker()
  Dim o1 As Task(Integer) = OnceWorker()
  Dim o2 As Task(Integer) = OnceWorker()
  Dim o3 As Task(Integer) = OnceWorker()
  Assert (Await o0) = 1, "Once worker 0 saw 1 run"
  Assert (Await o1) = 1, "Once worker 1 saw 1 run"
  Assert (Await o2) = 1, "Once worker 2 saw 1 run"
  Assert (Await o3) = 1, "Once worker 3 saw 1 run"
  Assert AtomicIntLoad(ByRef g_onceRuns) = 1, "Once executed initializer exactly once"

  g_wg = WgNew()
  g_wgSum = AtomicIntNew(0)
  WgAdd(ByRef g_wg, 4)
  Dim w0 As Task(Integer) = WgWorker(10)
  Dim w1 As Task(Integer) = WgWorker(20)
  Dim w2 As Task(Integer) = WgWorker(30)
  Dim w3 As Task(Integer) = WgWorker(40)
  WgWait(ByRef g_wg)
  Assert AtomicIntLoad(ByRef g_wgSum) = 100, "WaitGroup waited for all 4 workers"
  Await w0
  Await w1
  Await w2
  Await w3

  g_bar = BarrierNew(4)
  g_barLeaders = AtomicIntNew(0)
  g_barPhase1 = AtomicIntNew(0)
  g_barPhase2Ok = AtomicIntNew(0)
  Dim b0 As Task(Integer) = BarrierWorker()
  Dim b1 As Task(Integer) = BarrierWorker()
  Dim b2 As Task(Integer) = BarrierWorker()
  Dim b3 As Task(Integer) = BarrierWorker()
  Await b0
  Await b1
  Await b2
  Await b3
  Assert AtomicIntLoad(ByRef g_barPhase2Ok) = 4, "all 4 threads observed phase 1 complete after barrier"
  Assert AtomicIntLoad(ByRef g_barLeaders) = 2, "exactly 1 leader per barrier generation across 2 generations"
End Test

Test "Hosted pthread Mutex & CondVar under Async concurrency, plus arm-m cross-compile of atomics"
  Assert MutexInit(ByRef g_mtx), "MutexInit"
  Defer MutexDestroy(ByRef g_mtx)
  Assert MutexTryLock(ByRef g_mtx), "MutexTryLock when unlocked"
  Assert Not MutexTryLock(ByRef g_mtx), "MutexTryLock fails when locked"
  MutexUnlock(ByRef g_mtx)

  g_mtxCounter = 0
  Dim m0 As Task(Integer) = MutexWorker(2000)
  Dim m1 As Task(Integer) = MutexWorker(2000)
  Dim m2 As Task(Integer) = MutexWorker(2000)
  Await m0
  Await m1
  Await m2
  Assert g_mtxCounter = 6000, "Mutex protects concurrent counter"

  Assert MutexInit(ByRef g_condMtx), "CondVar MutexInit"
  Defer MutexDestroy(ByRef g_condMtx)
  Assert CondInit(ByRef g_cond), "CondInit"
  Defer CondDestroy(ByRef g_cond)
  g_condReady = 0
  Dim notif As Task(Integer) = CondNotifier()
  MutexLock(ByRef g_condMtx)
  While g_condReady = 0 Do
    CondWait(ByRef g_cond, ByRef g_condMtx)
  End While
  MutexUnlock(ByRef g_condMtx)
  Assert g_condReady = 1, "CondWait observed signal from notifier"
  Await notif

  ' Verify Type Analyzer error on invalid atomic type + freestanding arm-m compile of std/sync.vb
  Dim comp(768), tmpDir(256), srcPath(300), objPath(300) As Byte
  GetCompilerAbs(comp, 768)
  Dim pid As Integer = getpid()
  snprintf(tmpDir, 256, "/tmp/vb_sync_%d", pid)
  snprintf(srcPath, 300, "%s/bad_atomic.vb", tmpDir)
  snprintf(objPath, 300, "%s/sync_arm.o", tmpDir)
  Dim cmd(1536) As Byte
  snprintf(cmd, 1536, "mkdir -p '%s'", tmpDir)
  system(cmd)

  WriteTextFile(srcPath, "Public Function main() As Integer\n  Dim d As Double = 1.0\n  Dim x As Double = AtomicAdd(AddressOf(d), Double, 1.0)\n  Return 0\nEnd Function\n")
  snprintf(cmd, 1536, "'%s' '%s' --emit-only", comp, srcPath)
  Assert RunShell(cmd) <> 0, "AtomicAdd on Double must fail Type Analyzer"
  Assert OutHas("Atomic operation requires"), "error reports invalid atomic type"

  WriteTextFile(srcPath, "Include \"std/sync.vb\"\nPublic Function main() As Integer\n  Dim lk As SpinLock = SpinLockNew()\n  SpinLockAcquire(ByRef lk)\n  SpinLockRelease(ByRef lk)\n  Dim a As AtomicInt = AtomicIntNew(1)\n  Return AtomicIntAdd(ByRef a, 2)\nEnd Function\n")
  snprintf(cmd, 1536, "'%s' --target arm-m -c '%s' -o '%s'", comp, srcPath, objPath)
  Assert RunShell(cmd) = 0, "arm-m cross-compile with std/sync.vb atomics succeeds"

  snprintf(cmd, 1536, "rm -rf '%s'", tmpDir)
  system(cmd)
End Test
