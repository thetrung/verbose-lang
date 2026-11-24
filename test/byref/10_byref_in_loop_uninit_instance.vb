' Test 10: ByRef struct passed through loop calls + uninitialized Dim
Structure Stat
  Dim hits As Integer
  Dim score As Integer
End Structure

Sub RecordHit(ByRef s As Stat)
  s.hits = s.hits + 1
  s.score = s.score + 10
End Sub

Public Function main() As Integer
  Dim st As Stat
  st.hits = 0
  st.score = 0
  For i = 1 To 4
    RecordHit(ByRef st)
  End For
  printf("hits=%d score=%d\n", st.hits, st.score)
  Return 0
End Function
