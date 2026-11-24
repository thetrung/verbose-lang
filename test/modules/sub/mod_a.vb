Include "../common/shared.vb"

Public Function FromModA() As Integer
  BumpShared(5)
  Return ColorKind.Red
End Function
