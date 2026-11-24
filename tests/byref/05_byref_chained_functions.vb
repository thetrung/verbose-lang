' Test 05: Chained/forwarded ByRef across multiple Subs and Functions
Structure State
  Dim stage As Integer
  Dim total As Integer
End Structure

Function Finalize(ByRef s As State) As Integer
  s.stage = 3
  s.total = s.total + 100
  Return s.total
End Function

Sub Middle(ByRef s As State)
  s.stage = 2
  s.total = s.total + 20
  Dim res As Integer
  res = Finalize(ByRef s)
End Sub

Sub Outer(ByRef s As State)
  s.stage = 1
  s.total = s.total + 5
  Middle(ByRef s)
End Sub

Public Function main() As Integer
  Dim st As State(0, 0)
  Outer(ByRef st)
  Printf("stage=%d total=%d\n", st.stage, st.total)
  Return 0
End Function
