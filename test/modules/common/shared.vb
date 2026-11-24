Enum ColorKind
  Red = 1
  Green = 2
  Blue = 3
End Enum

Static g_shared_counter As Integer = 10

Public Sub BumpShared(delta As Integer)
  g_shared_counter = g_shared_counter + delta
End Sub
