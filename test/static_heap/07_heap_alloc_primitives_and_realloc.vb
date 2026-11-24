Public Function main() As Integer
  Dim nums() As Integer = HeapAlloc(Integer, 4)
  Printf("zero=%d,%d\n", nums(0), nums(3))
  For i = 0 To 3
    nums(i) = (i + 1) * 5
  End For
  nums = HeapRealloc(nums, Integer, 6)
  nums(4) = 25
  nums(5) = 30
  Printf("grown=%d,%d,%d,%d\n", nums(0), nums(3), nums(4), nums(5))
  HeapFree(nums)
  Return 0
End Function
