Private Function Classify(code As Integer) As Integer
  Select Case code
    Case 1, 2
      Return 100
    Case 3
      Return 200
    Case Else
      Return -1
  End Select
End Function

Public Function main() As Integer
  Printf("esc=\"hello\"\\t\tpath=C:\\vb\n")
  Printf("cls=%d,%d,%d\n", Classify(2), Classify(3), Classify(9))
  Return 0
End Function
