' VerBose Standard Library: Tagged Union Option(Of T) & Result(Of T, E) (std/result.vb)
' Zero-GC, inline tagged sum types for explicit optionality and error handling without sentinel codes.
' Zero libc dependencies (bare-metal / freestanding arm-m safe unless OptUnwrap/ResUnwrap asserts).

Public Union Option(Of T)
  Case None
  Case Some(value As T)
End Union

Public Union Result(Of T, E)
  Case Ok(value As T)
  Case Err(err As E)
End Union

' =============================================================================
' Option(Of T) Constructors & Helpers
' =============================================================================

Public Function OptNone(Of T)() As Option(Of T)
  Return Option(Of T).None
End Function

Public Function OptSome(Of T)(val As T) As Option(Of T)
  Return Option(Of T).Some(val)
End Function

Public Function OptIsSome(Of T)(o As Option(Of T)) As Boolean
  Match o With
    Case Some(_) Then Return True
    Case None Then Return False
  End Match
End Function

Public Function OptIsNone(Of T)(o As Option(Of T)) As Boolean
  Match o With
    Case None Then Return True
    Case Some(_) Then Return False
  End Match
End Function

Public Function OptUnwrapOr(Of T)(o As Option(Of T), defaultVal As T) As T
  Match o With
    Case Some(v) Then Return v
    Case None Then Return defaultVal
  End Match
End Function

Public Function OptUnwrap(Of T)(o As Option(Of T)) As T
  Match o With
    Case Some(v) Then Return v
    Case None Then
      Assert False, "OptUnwrap called on Option.None"
      Dim zeroVal As T
      Return zeroVal
  End Match
End Function

' =============================================================================
' Result(Of T, E) Constructors & Helpers
' =============================================================================

Public Function ResOk(Of T, E)(val As T) As Result(Of T, E)
  Return Result(Of T, E).Ok(val)
End Function

Public Function ResErr(Of T, E)(e As E) As Result(Of T, E)
  Return Result(Of T, E).Err(e)
End Function

Public Function ResIsOk(Of T, E)(r As Result(Of T, E)) As Boolean
  Match r With
    Case Ok(_) Then Return True
    Case Err(_) Then Return False
  End Match
End Function

Public Function ResIsErr(Of T, E)(r As Result(Of T, E)) As Boolean
  Match r With
    Case Err(_) Then Return True
    Case Ok(_) Then Return False
  End Match
End Function

Public Function ResUnwrapOr(Of T, E)(r As Result(Of T, E), defaultVal As T) As T
  Match r With
    Case Ok(v) Then Return v
    Case Err(_) Then Return defaultVal
  End Match
End Function

Public Function ResUnwrap(Of T, E)(r As Result(Of T, E)) As T
  Match r With
    Case Ok(v) Then Return v
    Case Err(_) Then
      Assert False, "ResUnwrap called on Result.Err"
      Dim zeroVal As T
      Return zeroVal
  End Match
End Function

Public Function ResUnwrapErr(Of T, E)(r As Result(Of T, E)) As E
  Match r With
    Case Err(e) Then Return e
    Case Ok(_) Then
      Assert False, "ResUnwrapErr called on Result.Ok"
      Dim zeroErr As E
      Return zeroErr
  End Match
End Function

Public Function ResOkOpt(Of T, E)(r As Result(Of T, E)) As Option(Of T)
  Match r With
    Case Ok(v) Then Return Option(Of T).Some(v)
    Case Err(_) Then Return Option(Of T).None
  End Match
End Function

Public Function ResErrOpt(Of T, E)(r As Result(Of T, E)) As Option(Of E)
  Match r With
    Case Err(e) Then Return Option(Of E).Some(e)
    Case Ok(_) Then Return Option(Of E).None
  End Match
End Function
