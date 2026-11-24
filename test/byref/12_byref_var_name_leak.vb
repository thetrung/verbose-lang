' Test 12: Cross-function var_types leak when a ByRef struct param name is reused as primitive
Structure Vector
  Dim x As Integer
End Structure

Sub TouchVec(ByRef v As Vector)
  v.x = 77
End Sub

Function ReuseName() As Integer
  Dim v As Integer = 12
  Return v
End Function

Public Function main() As Integer
  Dim vec As Vector(1)
  TouchVec(ByRef vec)
  Dim r As Integer
  r = ReuseName()
  printf("vec.x=%d r=%d\n", vec.x, r)
  Return 0
End Function
