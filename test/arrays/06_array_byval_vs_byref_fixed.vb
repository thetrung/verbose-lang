Private Sub MutateByVal(ByVal a(3) As Integer)
  a(0) = 999
End Sub

Private Sub MutateByRef(ByRef a(3) As Integer)
  a(0) = 777
End Sub

Public Function main() As Integer
  Dim nums(3) As Integer = (1, 2, 3)
  MutateByVal(nums)
  Dim afterVal As Integer = nums(0)
  MutateByRef(ByRef nums)
  Printf("after_byval=%d after_byref=%d\n", afterVal, nums(0))
  Return 0
End Function
