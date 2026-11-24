Include "lib_math.vb"

Public Function main() As Integer
  Dim s As Integer = AddNums(17, 25)
  Dim p1 As Vec2 With ( x = 3, y = 4 )
  Dim p2 As Vec2 With ( x = 10, y = 20 )
  Dim p3 As Vec2 = VecAdd(p1, p2)
  printf("sum=%d pt=%d,%d\n", s, p3.x, p3.y)
  Return 0
End Function
