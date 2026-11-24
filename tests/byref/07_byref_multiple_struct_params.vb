' Test 07: Multiple ByRef struct parameters (swap two instances)
Structure Pair
  Dim a As Integer
  Dim b As Long
End Structure

Sub SwapPairs(ByRef left As Pair, ByRef right As Pair)
  Dim tmp As Pair
  tmp = left
  left = right
  right = tmp
End Sub

Public Function main() As Integer
  Dim p1 As Pair(10, 100)
  Dim p2 As Pair(20, 200)
  SwapPairs(ByRef p1, ByRef p2)
  Printf("p1=%d,%ld p2=%d,%ld\n", p1.a, p1.b, p2.a, p2.b)
  Return 0
End Function
