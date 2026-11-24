' Test 06: Nested struct instance passed ByRef (outer struct + inner field struct)
Structure Vector
  Dim x As Single
  Dim y As Double
  Dim z As Long
End Structure

Structure Box
  Dim vec As Vector
  Dim tag As Integer
End Structure

Sub ReplaceBoxVec(ByRef b As Box)
  Dim nv As Vector(2.0, 3.0, 40)
  b.vec = nv
  b.tag = 99
End Sub

Sub MutateInnerVec(ByRef v As Vector)
  v.z = 888
End Sub

Public Function main() As Integer
  Dim v0 As Vector(1.0, 1.0, 1)
  Dim box As Box(v0, 7)
  ReplaceBoxVec(ByRef box)
  printf("after_box=tag:%d,z:%ld\n", box.tag, box.vec.z)
  MutateInnerVec(ByRef box.vec)
  printf("after_inner=tag:%d,z:%ld\n", box.tag, box.vec.z)
  Return 0
End Function
