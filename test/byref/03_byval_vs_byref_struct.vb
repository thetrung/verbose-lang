' Test 03: Contrast ByVal, default (ByVal), and ByRef on the same struct instance
Structure Pair
  Dim a As Integer
  Dim b As Long
End Structure

Sub TouchByVal(ByVal p As Pair)
  p.a = 111
  p.b = 222
End Sub

Sub TouchDefault(p As Pair)
  p.a = 333
  p.b = 444
End Sub

Sub TouchByRef(ByRef p As Pair)
  p.a = 555
  p.b = 666
End Sub

Public Function main() As Integer
  Dim inst As Pair(1, 2)
  TouchByVal(inst)
  printf("after_byval=%d,%ld\n", inst.a, inst.b)
  TouchDefault(inst)
  printf("after_default=%d,%ld\n", inst.a, inst.b)
  TouchByRef(inst)
  printf("after_byref=%d,%ld\n", inst.a, inst.b)
  Return 0
End Function
