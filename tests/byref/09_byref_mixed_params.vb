' Test 09: Mixed ByRef struct + ByVal primitive parameters in Function
Structure Accum
  Dim value As Long
End Structure

Function AddDelta(ByRef acc As Accum, ByVal delta As Long) As Long
  acc.value = acc.value + delta
  delta = 9999
  Return acc.value
End Function

Public Function main() As Integer
  Dim a As Accum(100)
  Dim d As Long = 25
  Dim out As Long
  out = AddDelta(ByRef a, d)
  Printf("acc=%ld delta=%ld out=%ld\n", a.value, d, out)
  Return 0
End Function
