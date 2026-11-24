' Leading comment
Private Function DoubleIt(n As Integer) As Integer ' inline comment on def
  Dim res As Integer = n * 2 ' inline comment on Dim
  res = res + 1 ' inline comment on Assign
  Return res ' inline comment on Return
End Function ' inline comment on End Function

Public Function main() As Integer
  Dim out As Integer = DoubleIt(20) ' inline call comment
  Printf("out=%d\n", out) ' inline printf comment
  Return 0
End Function
' Trailing comment with no final newline