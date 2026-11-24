Structure Point
  Dim x As Integer
  Dim y As Integer
End Structure

Private Function Sum(arr() As Integer, count As Integer) As Integer
  Dim total As Integer = 0
  For i = 0 To count - 1
    total = total + arr(i)
  End For
  Return total
End Function

Private Sub DoubleInPlace(ByRef arr() As Integer, count As Integer)
  For i = 0 To count - 1
    arr(i) = arr(i) * 2
  End For
End Sub

Public Function main() As Integer
  Dim nums() As Integer = {10, 20, 30, 40, 50}
  printf("len=%d sum=%d\n", Len(nums), Sum(nums, Len(nums)))

  DoubleInPlace(ByRef nums, Len(nums))
  printf("doubled_sum=%d last=%d\n", Sum(nums, Len(nums)), nums(4))

  Dim pts(2) As Point
  pts(0) = Point With ( x = 3, y = 4 )
  pts(1) = Point With ( x = 7, y = 8 )
  printf("pts=(%d,%d) (%d,%d)\n", pts(0).x, pts(0).y, pts(1).x, pts(1).y)
  Return 0
End Function
