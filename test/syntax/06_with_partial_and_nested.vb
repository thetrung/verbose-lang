Structure Vec2
  Dim x As Single
  Dim y As Double
End Structure

Structure Entity
  Dim id As Integer
  Dim pos As Vec2
End Structure

Public Function main() As Integer
  ' Partial initialization: omitted fields must be zero-initialized
  Dim partial As Vec2 With { y = 7.5 }

  ' Nested With initializer
  Dim e As Entity With {
    id = 10,
    pos = Vec2 With { x = 1.25, y = 2.5 }
  }
  printf("partial=%.2f,%.2f init=%d:%.2f,%.2f\n", partial.x, partial.y, e.id, e.pos.x, e.pos.y)

  ' Expression-level reassignment with With
  e.pos = Vec2 With { x = 9.0, y = 10.5 }
  e = Entity With { id = 20, pos = partial }
  printf("reassigned=%d:%.2f,%.2f\n", e.id, e.pos.x, e.pos.y)
  Return 0
End Function
