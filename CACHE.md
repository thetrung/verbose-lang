# verbose-lang — API & Module Cache

## Project Summary

**VerBose-Lang** (`v0.11.0`) is a brace-free, case-sensitive, Visual Basic–inspired systems programming language compiled directly to LLVM IR via `clang -O2` (zero GC, native C ABI, **100% self-hosted**). It targets Linux (`x86-64`, `arm64`), macOS (`arm64`), Windows (`x86-64`), and bare-metal 32-bit `arm-m`, shipping a 10-module self-hosted compiler (`src/`, including zero-overhead monomorphized generics in `src/generics.vb`) and an 8-module zero-GC, Arena-backed pure-VerBose standard library (`std/`).

---

## Standard Library (`std/`)

### `std/strings.vb` — Zero-GC Strings, Views & Builder (zero libc)
*Public types:* `StrView`, `StrBuilder`

| Function / Sub | Description |
| :--- | :--- |
| `StrLen(s As Pointer) As Integer` | Return byte length of a null-terminated string (`0` if null). |
| `StrCmp(a As Pointer, b As Pointer) As Integer` | Lexicographically compare two strings (`<0`, `0`, `>0`; null-safe). |
| `StrEq(a As Pointer, b As Pointer) As Boolean` | Return `True` if two strings have identical contents. |
| `StrStartsWith(s As Pointer, prefix As Pointer) As Boolean` | Return `True` if `s` starts with `prefix`. |
| `StrEndsWith(s As Pointer, suffix As Pointer) As Boolean` | Return `True` if `s` ends with `suffix`. |
| `StrIndexOf(s As Pointer, ch As Integer) As Integer` | Return first byte index of `ch` in `s`, or `-1` if not found. |
| `StrFind(haystack As Pointer, needle As Pointer) As Integer` | Return first byte index of substring `needle` in `haystack`, or `-1`. |
| `StrCopy(ByRef a As Arena, s As Pointer) As Pointer` | Duplicate string `s` into `Arena` `a`. |
| `StrSubstr(ByRef a As Arena, s As Pointer, start As Integer, length As Integer) As Pointer` | Allocate a null-terminated substring slice into `Arena` `a`. |
| `StrConcat(ByRef a As Arena, aStr As Pointer, bStr As Pointer) As Pointer` | Concatenate `aStr` and `bStr` into `Arena` `a`. |
| `StrTrim(ByRef a As Arena, s As Pointer) As Pointer` | Trim leading and trailing ASCII whitespace into `Arena` `a`. |
| `StrToUpper(ByRef a As Arena, s As Pointer) As Pointer` | Copy `s` with ASCII lowercase letters converted to uppercase in `a`. |
| `StrToLower(ByRef a As Arena, s As Pointer) As Pointer` | Copy `s` with ASCII uppercase letters converted to lowercase in `a`. |
| `StrToInt(s As Pointer) As Integer` | Parse a signed base-10 integer from `s` (skips leading whitespace). |
| `IntToStr(ByRef a As Arena, n As Integer) As Pointer` | Format signed integer `n` as a decimal string in `Arena` `a`. |
| `StrViewFrom(s As Pointer) As StrView` | Create a zero-copy `(ptr, len)` view over null-terminated string `s`. |
| `StrViewEq(a As StrView, b As StrView) As Boolean` | Compare two `StrView` slices for byte-for-byte equality. |
| `SbNew(ByRef a As Arena, cap As Integer) As StrBuilder` | Create an Arena-backed string builder with initial capacity `cap`. |
| `SbAppendChar(ByRef sb As StrBuilder, ch As Integer)` | Append a single ASCII byte `ch` to the builder. |
| `SbAppend(ByRef sb As StrBuilder, s As Pointer)` | Append null-terminated string `s` to the builder. |
| `SbAppendInt(ByRef sb As StrBuilder, n As Integer)` | Append signed integer `n` formatted in base 10 to the builder. |
| `SbFinish(ByRef sb As StrBuilder) As Pointer` | Null-terminate the builder buffer and return its `Pointer`. |
| `SbReset(ByRef sb As StrBuilder)` | Reset the builder to empty (retains capacity). |
| `SbAppendSlice(ByRef sb As StrBuilder, s As Pointer, length As Integer)` | Append exactly `length` bytes from `s` to the builder. |
| `SbAppendLine(ByRef sb As StrBuilder, s As Pointer)` | Append null-terminated string `s` followed by `\n` to the builder. |
| `SbAppendBool(ByRef sb As StrBuilder, b As Boolean)` | Append `True`/`False` to the builder. |
| `SbAppendLong(ByRef sb As StrBuilder, n As Long)` | Append signed `Long` `n` formatted in base 10 to the builder. |
| `SbAppendHex(ByRef sb As StrBuilder, n As Long)` | Append `Long` `n` formatted in hexadecimal to the builder. |
| `SbAppendDouble(ByRef sb As StrBuilder, v As Double, decimals As Integer)` | Append `Double` `v` formatted with `decimals` decimal places to the builder. |
| `BoolToStr(b As Boolean) As Pointer` | Return a C string for `b` (`"True"` / `"False"`). |
| `LongToStr(ByRef a As Arena, n As Long) As Pointer` | Format signed `Long` `n` as a decimal string in `Arena` `a`. |
| `HexToStr(ByRef a As Arena, n As Long) As Pointer` | Format `Long` `n` as a hexadecimal string in `Arena` `a`. |
| `DoubleToStr(ByRef a As Arena, v As Double, decimals As Integer) As Pointer` | Format `Double` `v` with `decimals` decimal places into `Arena` `a`. |
| `StrConcat3(ByRef a As Arena, s1, s2, s3 As Pointer) As Pointer` | Concatenate three strings into `Arena` `a`. |
| `StrConcat4(ByRef a As Arena, s1, s2, s3, s4 As Pointer) As Pointer` | Concatenate four strings into `Arena` `a`. |
| `StrJoin(ByRef a As Arena, parts() As Pointer, count As Integer, sep As Pointer) As Pointer` | Join `count` strings with separator `sep` into `Arena` `a`. |
| `StrRepeat(ByRef a As Arena, s As Pointer, times As Integer) As Pointer` | Repeat string `s` `times` times into `Arena` `a`. |
| `StrFmtArgs(ByRef a As Arena, fmt As Pointer, args() As Pointer, argCount As Integer) As Pointer` | Format `fmt` with positional `{}` placeholders (literal braces escaped as `{{`/`}}`) from `args` into `Arena` `a`. |
| `StrFmt1(ByRef a As Arena, fmt, a1 As Pointer) As Pointer` | Format `fmt` with one positional `{}` argument. |
| `StrFmt2(ByRef a As Arena, fmt, a1, a2 As Pointer) As Pointer` | Format `fmt` with two positional `{}` arguments. |
| `StrFmt3(ByRef a As Arena, fmt, a1, a2, a3 As Pointer) As Pointer` | Format `fmt` with three positional `{}` arguments. |

### `std/sync.vb` — Concurrency Primitives (LLVM `Atomic*` builtins, bare-metal safe)
*Public types:* `AtomicInt`, `AtomicLong`, `SpinLock`, `RwSpinLock`, `Once`, `WaitGroup`, `SpinBarrier`, `Mutex`, `CondVar`

| Function / Sub | Description |
| :--- | :--- |
| `AtomicIntNew(initial As Integer) As AtomicInt` | Create an `AtomicInt` with value `initial`. |
| `AtomicIntLoad(ByRef a As AtomicInt) As Integer` | Atomically load `a`. |
| `AtomicIntStore(ByRef a As AtomicInt, v As Integer)` | Atomically store `v` into `a`. |
| `AtomicIntAdd(ByRef a As AtomicInt, delta As Integer) As Integer` | Atomic read-modify-write add; returns the new value. |
| `AtomicIntSub(ByRef a As AtomicInt, delta As Integer) As Integer` | Atomic read-modify-write subtract; returns the new value. |
| `AtomicIntExchange(ByRef a As AtomicInt, desired As Integer) As Integer` | Atomically exchange value with `desired`; returns the old value. |
| `AtomicIntCas(ByRef a As AtomicInt, expected, desired As Integer) As Boolean` | Atomic compare-and-swap; returns `True` on success. |
| `AtomicLongNew` / `AtomicLongLoad` / `AtomicLongStore` / `AtomicLongAdd` / `AtomicLongSub` / `AtomicLongExchange` / `AtomicLongCas` | `Long`-width versions of the `AtomicInt` operations above. |
| `SpinLockNew() As SpinLock` | Create an unlocked spin lock. |
| `SpinLockTryAcquire(ByRef lk As SpinLock) As Boolean` | Non-blocking acquire; returns `True` if acquired. |
| `SpinLockAcquire(ByRef lk As SpinLock)` | Blocking acquire (spins while held). |
| `SpinLockRelease(ByRef lk As SpinLock)` | Release the spin lock. |
| `RwSpinNew() As RwSpinLock` | Create an unlocked read-write spin lock. |
| `RwSpinReadLock(ByRef rw As RwSpinLock)` | Acquire a shared read lock. |
| `RwSpinReadUnlock(ByRef rw As RwSpinLock)` | Release a read lock. |
| `RwSpinWriteLock(ByRef rw As RwSpinLock)` | Acquire an exclusive write lock. |
| `RwSpinWriteUnlock(ByRef rw As RwSpinLock)` | Release a write lock. |
| `OnceNew() As Once` | Create a new `Once`. |
| `OnceCall(ByRef o As Once, fn As SubPtr())` | Run `fn` exactly once (subsequent calls are no-ops). |
| `WgNew() As WaitGroup` | Create a new wait group. |
| `WgAdd(ByRef wg As WaitGroup, delta As Integer)` | Add `delta` to the wait group counter. |
| `WgDone(ByRef wg As WaitGroup)` | Decrement the wait group counter. |
| `WgWait(ByRef wg As WaitGroup)` | Block until the wait group counter reaches `0`. |
| `BarrierNew(partyCount As Integer) As SpinBarrier` | Create a spin barrier for `partyCount` parties. |
| `BarrierWait(ByRef b As SpinBarrier) As Boolean` | Wait at the barrier; returns `True` when all parties have arrived. |
| `MutexInit(ByRef m As Mutex) As Boolean` | Initialize a hosted POSIX/winpthreads mutex (hosted targets only). |
| `MutexDestroy(ByRef m As Mutex)` | Destroy a mutex. |
| `MutexLock(ByRef m As Mutex)` | Block until the mutex is acquired. |
| `MutexTryLock(ByRef m As Mutex) As Boolean` | Non-blocking mutex acquire. |
| `MutexUnlock(ByRef m As Mutex)` | Release the mutex. |
| `CondInit(ByRef c As CondVar) As Boolean` | Initialize a hosted condition variable. |
| `CondDestroy(ByRef c As CondVar)` | Destroy a condition variable. |
| `CondWait(ByRef c As CondVar, ByRef m As Mutex)` | Wait on the condition (releases `m`). |
| `CondSignal(ByRef c As CondVar)` | Wake one waiter. |
| `CondBroadcast(ByRef c As CondVar)` | Wake all waiters. |

### `std/result.vb` — Tagged Unions: `Option(Of T)` & `Result(Of T, E)` (zero libc, bare-metal safe)
*Public types:* `Option(Of T)`, `Result(Of T, E)`

| Function | Description |
| :--- | :--- |
| `OptNone(Of T)() As Option(Of T)` | Create an `Option` in the `None` state. |
| `OptSome(Of T)(val As T) As Option(Of T)` | Create an `Option` in the `Some(val)` state. |
| `OptIsSome(Of T)(o As Option(Of T)) As Boolean` | Return `True` if `o` is `Some`. |
| `OptIsNone(Of T)(o As Option(Of T)) As Boolean` | Return `True` if `o` is `None`. |
| `OptUnwrapOr(Of T)(o As Option(Of T), defaultVal As T) As T` | Return the contained value or `defaultVal` if `None`. |
| `OptUnwrap(Of T)(o As Option(Of T)) As T` | Return the contained value; asserts on `None`. |
| `ResOk(Of T, E)(val As T) As Result(Of T, E)` | Create a `Result` in the `Ok(val)` state. |
| `ResErr(Of T, E)(e As E) As Result(Of T, E)` | Create a `Result` in the `Err(e)` state. |
| `ResIsOk(Of T, E)(r As Result(Of T, E)) As Boolean` | Return `True` if `r` is `Ok`. |
| `ResIsErr(Of T, E)(r As Result(Of T, E)) As Boolean` | Return `True` if `r` is `Err`. |
| `ResUnwrapOr(Of T, E)(r As Result(Of T, E), defaultVal As T) As T` | Return the contained value or `defaultVal` if `Err`. |
| `ResUnwrap(Of T, E)(r As Result(Of T, E)) As T` | Return the contained value; asserts on `Err`. |
| `ResUnwrapErr(Of T, E)(r As Result(Of T, E)) As E` | Return the error value; asserts on `Ok`. |
| `ResOkOpt(Of T, E)(r As Result(Of T, E)) As Option(Of T)` | Convert `Result` to `Option`, `Err` → `None`. |
| `ResErrOpt(Of T, E)(r As Result(Of T, E)) As Option(Of E)` | Convert `Result` to `Option`, `Ok` → `None`. |

### `std/math.vb` — Pure-VerBose Math (zero `-lm`)
*Public constants:* `MATH_PI`, `MATH_TAU`, `MATH_E`, `MATH_HALF_PI`, `MATH_EPS`

| Function | Description |
| :--- | :--- |
| `MathAbsI(x As Integer) As Integer` | Absolute value of 32-bit `Integer`. |
| `MathMinI(a As Integer, b As Integer) As Integer` | Minimum of two `Integer` values. |
| `MathMaxI(a As Integer, b As Integer) As Integer` | Maximum of two `Integer` values. |
| `MathClampI(x As Integer, lo As Integer, hi As Integer) As Integer` | Clamp `Integer` `x` to `[lo, hi]`. |
| `MathSignI(x As Integer) As Integer` | Return `-1`, `0`, or `1` for the sign of `x`. |
| `MathGcd(a As Integer, b As Integer) As Integer` | Greatest common divisor using Euclidean algorithm. |
| `MathLcm(a As Integer, b As Integer) As Integer` | Least common multiple of `a` and `b`. |
| `MathPowI(base As Integer, exp As Integer) As Integer` | Integer exponentiation by squaring (`exp >= 0`). |
| `MathIsqrt(n As Integer) As Integer` | Floor integer square root of non-negative `n`. |
| `MathAbsL(x As Long) As Long` | Absolute value of 64-bit `Long`. |
| `MathMinL(a As Long, b As Long) As Long` | Minimum of two `Long` values. |
| `MathMaxL(a As Long, b As Long) As Long` | Maximum of two `Long` values. |
| `MathClampL(x As Long, lo As Long, hi As Long) As Long` | Clamp `Long` `x` to `[lo, hi]`. |
| `MathAbsD(x As Double) As Double` | Absolute value of `Double`. |
| `MathMinD(a As Double, b As Double) As Double` | Minimum of two `Double` values. |
| `MathMaxD(a As Double, b As Double) As Double` | Maximum of two `Double` values. |
| `MathClampD(x As Double, lo As Double, hi As Double) As Double` | Clamp `Double` `x` to `[lo, hi]`. |
| `MathFloor(x As Double) As Double` | Largest integral `Double` `<= x`. |
| `MathCeil(x As Double) As Double` | Smallest integral `Double` `>= x`. |
| `MathRound(x As Double) As Double` | Round `Double` to nearest integer value. |
| `MathSqrt(x As Double) As Double` | Double-precision square root via Newton-Raphson. |
| `MathSin(x As Double) As Double` | Sine of angle `x` (radians) via range-reduced Taylor series. |
| `MathCos(x As Double) As Double` | Cosine of angle `x` (radians). |
| `MathTan(x As Double) As Double` | Tangent of angle `x` (radians). |
| `MathExp(x As Double) As Double` | Exponential function $e^x$. |
| `MathLn(x As Double) As Double` | Natural logarithm $\ln(x)$ for `x > 0`. |
| `MathPow(base As Double, exp As Double) As Double` | Double-precision power $\text{base}^{\text{exp}}$. |
| `MathLerp(a As Double, b As Double, t As Double) As Double` | Linear interpolation `a + (b - a) * t`. |
| `MathNearlyEq(a As Double, b As Double, eps As Double) As Boolean` | Return `True` if `|a - b| <= eps`. |
| `MathDegToRad(deg As Double) As Double` | Convert degrees to radians. |
| `MathRadToDeg(rad As Double) As Double` | Convert radians to degrees. |

### `std/collections.vb` — Arena-Backed Data Structures
*Public types:* `Vec(Of T)`, `Ring(Of T)`, `IntVec`, `PtrVec`, `IntRing`, `IntMap`

| Function / Sub | Description |
| :--- | :--- |
| `VecNew(Of T)(ByRef a As Arena, cap As Integer) As Vec(Of T)` | Allocate a generic Arena-backed typed vector with capacity `cap`. |
| `VecPush(Of T)(ByRef v As Vec(Of T), val As T) As Boolean` | Append `val` to `Vec(Of T)`; returns `False` if full. |
| `VecPop(Of T)(ByRef v As Vec(Of T), ByRef outVal As T) As Boolean` | Pop last element into `outVal`; returns `False` if empty. |
| `VecGet(Of T)(ByRef v As Vec(Of T), idx As Integer) As T` | Read element at `idx` (zero-initialized `T` if out of bounds). |
| `VecSet(Of T)(ByRef v As Vec(Of T), idx As Integer, val As T) As Boolean` | Write `val` at `idx`; returns `False` if out of bounds. |
| `VecClear(Of T)(ByRef v As Vec(Of T))` | Reset `Vec(Of T)` length to `0`. |
| `RingNew(Of T)(ByRef a As Arena, cap As Integer) As Ring(Of T)` | Allocate a generic Arena-backed FIFO ring buffer with capacity `cap`. |
| `RingPush(Of T)(ByRef r As Ring(Of T), val As T) As Boolean` | Enqueue `val` at ring tail; returns `False` if full. |
| `RingPop(Of T)(ByRef r As Ring(Of T), ByRef outVal As T) As Boolean` | Dequeue oldest element into `outVal`; returns `False` if empty. |
| `RingLen(Of T)(ByRef r As Ring(Of T)) As Integer` | Return number of queued elements. |
| `RingIsEmpty(Of T)(ByRef r As Ring(Of T)) As Boolean` | Return `True` if ring buffer is empty. |
| `RingIsFull(Of T)(ByRef r As Ring(Of T)) As Boolean` | Return `True` if ring buffer is at capacity. |
| `IntVecNew(ByRef a As Arena, cap As Integer) As IntVec` | Allocate an Arena-backed dynamic `Integer` vector with capacity `cap`. |
| `IntVecPush(ByRef v As IntVec, val As Integer) As Boolean` | Append `val` to `IntVec`; returns `False` if full. |
| `IntVecPop(ByRef v As IntVec, ByRef outVal As Integer) As Boolean` | Pop last element into `outVal`; returns `False` if empty. |
| `IntVecGet(ByRef v As IntVec, idx As Integer) As Integer` | Read element at `idx` (`0` if out of bounds). |
| `IntVecSet(ByRef v As IntVec, idx, val As Integer) As Boolean` | Write `val` at `idx`; returns `False` if out of bounds. |
| `IntVecClear(ByRef v As IntVec)` | Reset `IntVec` length to `0` (retains capacity). |
| `IntVecContains(ByRef v As IntVec, val As Integer) As Boolean` | Linear search for `val` in `IntVec`. |
| `IntVecSort(ByRef v As IntVec)` | Sort `IntVec` elements ascending in place. |
| `PtrVecNew(ByRef a As Arena, cap As Integer) As PtrVec` | Allocate an Arena-backed dynamic `Pointer` vector with capacity `cap`. |
| `PtrVecPush(ByRef v As PtrVec, val As Pointer) As Boolean` | Append pointer `val` to `PtrVec`; returns `False` if full. |
| `PtrVecPop(ByRef v As PtrVec, ByRef outVal As Pointer) As Boolean` | Pop last pointer into `outVal`; returns `False` if empty. |
| `PtrVecGet(ByRef v As PtrVec, idx As Integer) As Pointer` | Read pointer at `idx` (`0`/null if out of bounds). |
| `PtrVecSet(ByRef v As PtrVec, idx As Integer, val As Pointer) As Boolean` | Write pointer `val` at `idx`; returns `False` if out of bounds. |
| `PtrVecClear(ByRef v As PtrVec)` | Reset `PtrVec` length to `0`. |
| `IntRingNew(ByRef a As Arena, cap As Integer) As IntRing` | Allocate an Arena-backed fixed-capacity FIFO `Integer` ring buffer. |
| `IntRingPush(ByRef r As IntRing, val As Integer) As Boolean` | Enqueue `val` at ring tail; returns `False` if full. |
| `IntRingPop(ByRef r As IntRing, ByRef outVal As Integer) As Boolean` | Dequeue oldest element into `outVal`; returns `False` if empty. |
| `IntRingLen(ByRef r As IntRing) As Integer` | Return number of queued elements. |
| `IntRingIsEmpty(ByRef r As IntRing) As Boolean` | Return `True` if ring buffer is empty. |
| `IntRingIsFull(ByRef r As IntRing) As Boolean` | Return `True` if ring buffer is at capacity. |
| `IntMapNew(ByRef a As Arena, cap As Integer) As IntMap` | Allocate an open-addressing `Integer -> Integer` hash map in `a`. |
| `IntMapPut(ByRef m As IntMap, key, val As Integer) As Boolean` | Insert or update `key -> val`; returns `False` if table is full. |
| `IntMapGet(ByRef m As IntMap, key As Integer, ByRef outVal As Integer) As Boolean` | Lookup `key` and write value into `outVal`; returns `True` if found. |
| `IntMapHas(ByRef m As IntMap, key As Integer) As Boolean` | Return `True` if `key` exists in `m`. |
| `IntMapRemove(ByRef m As IntMap, key As Integer) As Boolean` | Delete `key` with tombstone marker; returns `True` if removed. |
| `IntMapLen(ByRef m As IntMap) As Integer` | Return number of active entries in `m`. |

### `std/io.vb` — File & Console I/O
*Public types:* `IoBuf`

| Function / Sub | Description |
| :--- | :--- |
| `IoReadFile(ByRef a As Arena, path As Pointer) As IoBuf` | Read entire file at `path` into an Arena-allocated null-terminated `IoBuf`. |
| `IoWriteFile(path, data As Pointer, length As Integer) As Boolean` | Write `length` raw bytes from `data` to `path` (overwrite). |
| `IoWriteText(path, text As Pointer) As Boolean` | Write null-terminated string `text` to `path` (overwrite). |
| `IoAppendText(path, text As Pointer) As Boolean` | Append null-terminated string `text` to `path`. |
| `IoFileExists(path As Pointer) As Boolean` | Return `True` if `path` can be opened for reading. |
| `IoFileSize(path As Pointer) As Long` | Return file size in bytes, or `-1` on error. |
| `IoRemoveFile(path As Pointer) As Boolean` | Delete file at `path`; returns `True` on success. |
| `IoPrint(s As Pointer)` | Print null-terminated string `s` to stdout without newline. |
| `IoPrintLn(s As Pointer)` | Print null-terminated string `s` followed by `\n` to stdout. |
| `IoReadLine(ByRef a As Arena, fp As Pointer) As IoBuf` | Read one line from `FILE*` `fp` into an Arena-allocated `IoBuf`. |

### `std/json.vb` — Strict RFC 8259 JSON DOM & Streaming Writer (zero libc, bare-metal safe)
*Public types:* `JsonKind` (`JsonInvalid`, `JsonNull`, `JsonBool`, `JsonInt`, `JsonFloat`, `JsonString`, `JsonArray`, `JsonObject`), `JsonNode`, `JsonDoc`, `JsonWriter`

| Function / Sub | Description |
| :--- | :--- |
| `JsonParse(ByRef a As Arena, src As Pointer) As JsonDoc` | Strictly parse RFC 8259 JSON text into a flat Arena-backed node pool (default 2048-node cap). |
| `JsonParseWithCap(ByRef a As Arena, src As Pointer, maxNodes As Integer) As JsonDoc` | Parse JSON text with an explicit node-pool capacity. |
| `JsonIsNull` / `JsonIsBool` / `JsonIsInt` / `JsonIsFloat` / `JsonIsNumber` / `JsonIsString` / `JsonIsArray` / `JsonIsObject` | Type predicates on `(ByRef doc As JsonDoc, id As Integer)`. |
| `JsonAsBool` / `JsonAsInt` / `JsonAsLong` / `JsonAsDouble` / `JsonAsString` / `JsonStrLen` | Extract typed values (with `Int`↔`Double` coercion for numbers and byte length for strings). |
| `JsonLen` / `JsonAt` / `JsonGet` / `JsonHas` / `JsonFirst` / `JsonNext` / `JsonKey` | Array indexing, object key lookup (last duplicate wins), and linked-sibling iteration. |
| `JsonGetString` / `JsonGetInt` / `JsonGetLong` / `JsonGetDouble` / `JsonGetBool` | Typed object-field getters with explicit default fallback values. |
| `JsonDocNew` / `JsonNewNull` / `JsonNewBool` / `JsonNewInt` / `JsonNewLong` / `JsonNewFloat` / `JsonNewString` / `JsonNewArray` / `JsonNewObject` | Programmatic Arena-backed DOM constructors. |
| `JsonArrayPush` / `JsonObjectSet` / `JsonStringify` | Mutate DOM arrays/objects and serialize any node back to compact RFC 8259 JSON in an `Arena`. |
| `JwNew` / `JwBeginObj` / `JwEndObj` / `JwBeginArr` / `JwEndArr` / `JwKey` / `JwNull` / `JwBool` / `JwInt` / `JwLong` / `JwDouble` / `JwString` / `JwStringBytes` / `JwRaw` / `JwField*` / `JwFinish` | Zero-DOM streaming JSON emitter with automatic comma management, control-byte escaping, non-finite float → `null`, and overflow protection. |

### `std/span.vb` — Explicit Bounds-Checked Spans (zero libc, bare-metal safe)
*Public types:* `Span(Of T)`, `ByteSpan`, `IntSpan`, `LongSpan`, `SingleSpan`, `PtrSpan`

| Function Family (`Span(Of T)` + concrete `Byte`/`Int`/`Long`/`Single`/`Ptr` spans) | Description |
| :--- | :--- |
| `SpanFrom(Of T)(buf As Pointer, length As Long) As Span(Of T)` | Wrap raw buffer pointer and explicit element count into a generic `Span(Of T)`. |
| `SpanInBounds(Of T)(s As Span(Of T), idx As Long) As Boolean` | Return `True` if `0 <= idx < s.length` and `s.data` is non-null. |
| `SpanGet(Of T)(s As Span(Of T), idx As Long, ByRef outVal As T) As Boolean` | Bounds-checked read into `outVal`; returns `False` on out-of-bounds. |
| `SpanSet(Of T)(s As Span(Of T), idx As Long, val As T) As Boolean` | Bounds-checked write of `val` at `idx`; returns `False` on out-of-bounds. |
| `SpanAt(Of T)(s As Span(Of T), idx As Long) As T` | Asserting bounds-checked element read at `idx`. |
| `SpanSub(Of T)(s As Span(Of T), start, count As Long) As Span(Of T)` | Return clamped sub-span starting at `start` with up to `count` elements. |
| `<T>SpanFrom(buf As Pointer, length As Long) As <T>Span` | Wrap raw buffer pointer and explicit element count into a span (clamps negative/null to length `0`). |
| `<T>SpanInBounds(s As <T>Span, idx As Long) As Boolean` | Return `True` if `0 <= idx < s.length` and `s.data` is non-null. |
| `<T>SpanGet(s As <T>Span, idx As Long, ByRef outVal As <Elem>) As Boolean` | Bounds-checked read into `outVal`; returns `False` on out-of-bounds. |
| `<T>SpanSet(s As <T>Span, idx As Long, val As <Elem>) As Boolean` | Bounds-checked write of `val` at `idx`; returns `False` on out-of-bounds. |
| `<T>SpanAt(s As <T>Span, idx As Long) As <Elem>` | Bounds-checked read returning zero/null default on out-of-bounds. |
| `<T>SpanSub(s As <T>Span, start, count As Long) As <T>Span` | Return clamped sub-span starting at `start` with up to `count` elements. |

---

## Compiler Pipeline Modules (`src/`)

| Module | Public Functions / Subs | Description |
| :--- | :--- | :--- |
| `src/verbose.vb` | `main(argc As Integer, argv() As Pointer) As Integer` | CLI driver entry point (flag parsing, host target auto-detection, pipeline dispatch, `verbose test` runner, `clang -O2` invocation). |
| `src/constants.vb` | *(Public `TargetKind` enum, diagnostic strings `MSG_*`, target datalayouts/triples, LLVM IR runtime templates `IR_*`)* | Shared compiler version, target metadata, error messages, and inline LLVM IR runtime templates. |
| `src/token.vb` | *(Public `TokenKind` enum, `Token` structure)* | Lexical token definitions and token record layout. |
| `src/lexer.vb` | `LexerClearIncludeDirs()` | Clear registered `-I` include search directories. |
| `src/lexer.vb` | `LexerAddIncludeDir(dirPtr As Pointer)` | Append an include search directory (max 32). |
| `src/lexer.vb` | `SliceEqualsStr(pos, slen As Integer, lit As Pointer, litLen As Integer) As Boolean` | Compare a slice in `g_srcPool` against a C string literal. |
| `src/lexer.vb` | `SlicesEqual(p1, l1, p2, l2 As Integer) As Boolean` | Compare two slices inside `g_srcPool` for equality. |
| `src/lexer.vb` | `LexerLoadFile(ByRef arena As Arena, pathPos, pathLen As Integer) As Integer` | Resolve, deduplicate, load, and lex an included `.vb` file. |
| `src/lexer.vb` | `LexerLoadEntry(ByRef arena As Arena, entryPath As Pointer) As Integer` | Initialize source/token pools and lex the entry `.vb` translation unit plus recursive `Include`s. |
| `src/ast.vb` | `AstInit(ByRef arena As Arena)` | Allocate flat AST node (`g_nodes`) and child-index (`g_kids`) pools in `arena`. |
| `src/ast.vb` | `NewNode(kind As Integer) As Integer` | Allocate a new `AstNode` of `kind` and return its pool index. |
| `src/ast.vb` | `PushKid(nodeId As Integer) As Integer` | Append `nodeId` to `g_kids` and return its slot index. |
| `src/parser.vb` | `ParseExpr() As Integer` | Parse an expression and return its AST node index. |
| `src/parser.vb` | `ParseStmt() As Integer` | Parse a single statement and return its AST node index. |
| `src/parser.vb` | `ParseProgram() As Integer` | Parse all top-level declarations/tests into a root program AST node. |
| `src/printer.vb` | `PrintProgramAst(progRoot As Integer)` | Pretty-print the parsed AST to stdout (`--ast`). |
| `src/generics.vb` | `MonomorphizeProgram(ByRef arena As Arena, progRoot As Integer) As Integer` | Monomorphize parametric `(Of T, ...)` `Structure`, `Function`, `Sub`, `Async`, and `Operator` templates into concrete AST definitions; returns error count. |
| `src/type_analyzer.vb` | `TaSetTarget(targetId As Integer)` | Configure active target architecture for ABI sizing and default stack budgets. |
| `src/type_analyzer.vb` | `TaSetMaxStack(bytes As Long)` | Override maximum per-function stack-frame budget in bytes (`0` disables). |
| `src/type_analyzer.vb` | `AnalyzeProgram(ByRef arena As Arena, progRoot As Integer) As Integer` | Run two-pass semantic type checking, constant fixed-array OOB checks, and stack-frame budget enforcement; returns error count. |
| `src/codegen.vb` | `CgSetTestMode(enabled As Integer)` | Enable/disable `verbose test` harness synthesis mode. |
| `src/codegen.vb` | `CgSetBoundsCheck(enabled As Integer)` | Enable/disable opt-in runtime fixed-array bounds checks (`--bounds-check`). |
| `src/codegen.vb` | `CgSetDebugInfo(enabled As Integer)` | Enable/disable DWARF debug metadata emission (`-g` / `--debug`, compiles with `clang -O0 -g`). |
| `src/codegen.vb` | `CgUsesAsync() As Integer` | Return `1` if the compiled module uses `Async`/`Await` (triggers `-lpthread` linking). |
| `src/codegen.vb` | `CgSetTarget(targetId As Integer)` | Configure LLVM target triple, datalayout, pointer width, and C struct ABI lowering. |
| `src/codegen.vb` | `EmitProgramToFile(ByRef arena As Arena, progRoot As Integer, outPath As Pointer) As Integer` | Emit complete LLVM IR (`.ll`) for `progRoot` to `outPath`; returns `0` on success. |
