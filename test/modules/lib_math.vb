Structure Vec2
  Dim x As Integer
  Dim y As Integer
End Structure

Public Function VecAdd(a As Vec2, b As Vec2) As Vec2
  Dim r As Vec2 With ( x = a.x + b.x, y = a.y + b.y )
  Return r
End Function

Public Function AddNums(a As Integer, b As Integer) As Integer
  Return a + b
End Function
