' Test 04: Function (with return value) mutating a ByRef struct instance
Structure Counter
  Dim count As Long
  Dim stepVal As Long
End Structure

Function Bump(ByRef c As Counter) As Long
  Dim nextVal As Long
  nextVal = c.count + c.stepVal
  c.count = nextVal
  Return nextVal
End Function

Public Function main() As Integer
  Dim ctr As Counter(10, 5)
  Dim r1 As Long
  Dim r2 As Long
  r1 = Bump(ByRef ctr)
  r2 = Bump(ByRef ctr)
  printf("r1=%ld r2=%ld final=%ld\n", r1, r2, ctr.count)
  Return 0
End Function
