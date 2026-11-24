Public Function main() As Integer
  Dim nums(5) As Integer
  Printf("init=%d,%d len=%d\n", nums(0), nums(4), Len(nums))
  For i = 0 To 4
    nums(i) = (i + 1) * 10
  End For
  Printf("vals=%d,%d,%d\n", nums(0), nums(2), nums(4))
  Return 0
End Function
