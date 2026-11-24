' Pure VerBose Standard Math Library (zero C -lm dependency).
' All Public symbols are prefixed with Math* or MATH_*.

Public Static MATH_PI As Double = 3.14159265358979323846
Public Static MATH_TAU As Double = 6.28318530717958647692
Public Static MATH_E As Double = 2.71828182845904523536
Public Static MATH_HALF_PI As Double = 1.57079632679489661923
Public Static MATH_EPS As Double = 0.000000000001

Private Static MATH_LN2 As Double = 0.69314718055994530942
Private Static MATH_SQRT2 As Double = 1.41421356237309504880
Private Static MATH_SQRT_HALF As Double = 0.70710678118654752440
Private Static MATH_TWO_POW_52 As Double = 4503599627370496.0

' --- Integer operations ---

Public Function MathAbsI(x As Integer) As Integer
  If x < 0 Then
    Return -x
  End If
  Return x
End Function

Public Function MathMinI(a As Integer, b As Integer) As Integer
  If a < b Then
    Return a
  End If
  Return b
End Function

Public Function MathMaxI(a As Integer, b As Integer) As Integer
  If a > b Then
    Return a
  End If
  Return b
End Function

Public Function MathClampI(x As Integer, lo As Integer, hi As Integer) As Integer
  If x < lo Then
    Return lo
  End If
  If x > hi Then
    Return hi
  End If
  Return x
End Function

Public Function MathSignI(x As Integer) As Integer
  If x < 0 Then
    Return -1
  End If
  If x > 0 Then
    Return 1
  End If
  Return 0
End Function

Public Function MathGcd(a As Integer, b As Integer) As Integer
  Dim x As Integer = MathAbsI(a)
  Dim y As Integer = MathAbsI(b)
  While y <> 0 Do
    Dim t As Integer = x Mod y
    x = y
    y = t
  End While
  Return x
End Function

Public Function MathLcm(a As Integer, b As Integer) As Integer
  If a = 0 Then
    Return 0
  End If
  If b = 0 Then
    Return 0
  End If
  Dim g As Integer = MathGcd(a, b)
  Return (MathAbsI(a) / g) * MathAbsI(b)
End Function

Public Function MathPowI(base As Integer, exp As Integer) As Integer
  If exp < 0 Then
    Return 0
  End If
  Dim res As Integer = 1
  Dim b As Integer = base
  Dim e As Integer = exp
  While e > 0 Do
    If (e Mod 2) = 1 Then
      res = res * b
    End If
    e = e / 2
    If e > 0 Then
      b = b * b
    End If
  End While
  Return res
End Function

Public Function MathIsqrt(n As Integer) As Integer
  If n <= 0 Then
    Return 0
  End If
  Dim ln As Long = n
  Dim x As Long = ln
  Dim y As Long = (x + 1) / 2
  While y < x Do
    x = y
    y = (x + ln / x) / 2
  End While
  Dim res As Integer = x
  Return res
End Function

' --- Long operations ---

Public Function MathAbsL(x As Long) As Long
  If x < 0 Then
    Return -x
  End If
  Return x
End Function

Public Function MathMinL(a As Long, b As Long) As Long
  If a < b Then
    Return a
  End If
  Return b
End Function

Public Function MathMaxL(a As Long, b As Long) As Long
  If a > b Then
    Return a
  End If
  Return b
End Function

Public Function MathClampL(x As Long, lo As Long, hi As Long) As Long
  If x < lo Then
    Return lo
  End If
  If x > hi Then
    Return hi
  End If
  Return x
End Function

' --- Double operations ---

Public Function MathAbsD(x As Double) As Double
  If x < 0.0 Then
    Return -x
  End If
  Return x
End Function

Public Function MathMinD(a As Double, b As Double) As Double
  If a < b Then
    Return a
  End If
  Return b
End Function

Public Function MathMaxD(a As Double, b As Double) As Double
  If a > b Then
    Return a
  End If
  Return b
End Function

Public Function MathClampD(x As Double, lo As Double, hi As Double) As Double
  If x < lo Then
    Return lo
  End If
  If x > hi Then
    Return hi
  End If
  Return x
End Function

Private Function mathFloorPos(x As Double) As Double
  If x < 1.0 Then
    Return 0.0
  End If
  Dim p As Double = 1.0
  While (p * 2.0) <= x Do
    p = p * 2.0
  End While
  Dim rem As Double = x
  Dim acc As Double = 0.0
  While p >= 1.0 Do
    If rem >= p Then
      rem = rem - p
      acc = acc + p
    End If
    p = p * 0.5
  End While
  Return acc
End Function

Public Function MathFloor(x As Double) As Double
  If MathAbsD(x) >= MATH_TWO_POW_52 Then
    Return x
  End If
  If x >= 0.0 Then
    Return mathFloorPos(x)
  End If
  Dim pos As Double = -x
  Dim f As Double = mathFloorPos(pos)
  If f = pos Then
    Return -f
  End If
  Return -f - 1.0
End Function

Public Function MathCeil(x As Double) As Double
  If MathAbsD(x) >= MATH_TWO_POW_52 Then
    Return x
  End If
  Return -MathFloor(-x)
End Function

Public Function MathRound(x As Double) As Double
  If x >= 0.0 Then
    Return MathFloor(x + 0.5)
  End If
  Return MathCeil(x - 0.5)
End Function

Public Function MathSqrt(x As Double) As Double
  If x <= 0.0 Then
    Return 0.0
  End If
  Dim m As Double = x
  Dim s As Double = 1.0
  While m >= 4.0 Do
    m = m * 0.25
    s = s * 2.0
  End While
  While m < 0.25 Do
    m = m * 4.0
    s = s * 0.5
  End While
  Dim g As Double = 0.5 * (1.0 + m)
  Dim i As Integer = 0
  While i < 8 Do
    g = 0.5 * (g + m / g)
    i = i + 1
  End While
  Return g * s
End Function

Private Function mathSinReduced(r As Double) As Double
  Dim r2 As Double = r * r
  Dim sum As Double = r
  Dim term As Double = r
  Dim f1 As Double = 2.0
  Dim f2 As Double = 3.0
  Dim i As Integer = 1
  While i <= 10 Do
    term = -term * r2 / (f1 * f2)
    sum = sum + term
    f1 = f1 + 2.0
    f2 = f2 + 2.0
    i = i + 1
  End While
  Return sum
End Function

Private Function mathCosReduced(r As Double) As Double
  Dim r2 As Double = r * r
  Dim sum As Double = 1.0
  Dim term As Double = 1.0
  Dim f1 As Double = 1.0
  Dim f2 As Double = 2.0
  Dim i As Integer = 1
  While i <= 10 Do
    term = -term * r2 / (f1 * f2)
    sum = sum + term
    f1 = f1 + 2.0
    f2 = f2 + 2.0
    i = i + 1
  End While
  Return sum
End Function

Public Function MathSin(x As Double) As Double
  Dim k As Double = MathRound(x / MATH_TAU)
  Dim r As Double = x - k * MATH_TAU
  If r > MATH_HALF_PI Then
    r = MATH_PI - r
  Else
    If r < -MATH_HALF_PI Then
      r = -MATH_PI - r
    End If
  End If
  Return mathSinReduced(r)
End Function

Public Function MathCos(x As Double) As Double
  Dim k As Double = MathRound(x / MATH_TAU)
  Dim r As Double = x - k * MATH_TAU
  If r > MATH_HALF_PI Then
    Return -mathCosReduced(MATH_PI - r)
  End If
  If r < -MATH_HALF_PI Then
    Return -mathCosReduced(-MATH_PI - r)
  End If
  Return mathCosReduced(r)
End Function

Public Function MathTan(x As Double) As Double
  Dim c As Double = MathCos(x)
  If MathAbsD(c) < 0.000000000000001 Then
    Return 0.0
  End If
  Return MathSin(x) / c
End Function

Public Function MathExp(x As Double) As Double
  If x > 709.0 Then
    Return 0.0
  End If
  If x < -745.0 Then
    Return 0.0
  End If
  Dim kf As Double = MathRound(x / MATH_LN2)
  Dim r As Double = x - kf * MATH_LN2
  Dim sum As Double = 1.0
  Dim term As Double = 1.0
  Dim fi As Double = 1.0
  Dim i As Integer = 1
  While i <= 16 Do
    term = term * r / fi
    sum = sum + term
    fi = fi + 1.0
    i = i + 1
  End While
  If kf > 0.0 Then
    Dim j As Double = 0.0
    While j < kf Do
      sum = sum * 2.0
      j = j + 1.0
    End While
  Else
    If kf < 0.0 Then
      Dim negK As Double = -kf
      Dim j2 As Double = 0.0
      While j2 < negK Do
        sum = sum * 0.5
        j2 = j2 + 1.0
      End While
    End If
  End If
  Return sum
End Function

Public Function MathLn(x As Double) As Double
  If x <= 0.0 Then
    Return 0.0
  End If
  Dim m As Double = x
  Dim kd As Double = 0.0
  While m > MATH_SQRT2 Do
    m = m * 0.5
    kd = kd + 1.0
  End While
  While m < MATH_SQRT_HALF Do
    m = m * 2.0
    kd = kd - 1.0
  End While
  Dim y As Double = (m - 1.0) / (m + 1.0)
  Dim y2 As Double = y * y
  Dim sum As Double = 0.0
  Dim powY As Double = y
  Dim denom As Double = 1.0
  Dim i As Integer = 0
  While i < 12 Do
    sum = sum + powY / denom
    powY = powY * y2
    denom = denom + 2.0
    i = i + 1
  End While
  Return 2.0 * sum + kd * MATH_LN2
End Function

Public Function MathPow(base As Double, exp As Double) As Double
  If exp = 0.0 Then
    Return 1.0
  End If
  If base = 1.0 Then
    Return 1.0
  End If
  If base = 0.0 Then
    Return 0.0
  End If
  If base < 0.0 Then
    If MathFloor(exp) <> exp Then
      Return 0.0
    End If
    Dim mag As Double = MathExp(exp * MathLn(-base))
    Dim halfExp As Double = exp * 0.5
    If MathFloor(halfExp) * 2.0 <> exp Then
      Return -mag
    End If
    Return mag
  End If
  Return MathExp(exp * MathLn(base))
End Function

Public Function MathLerp(a As Double, b As Double, t As Double) As Double
  Return a + (b - a) * t
End Function

Public Function MathNearlyEq(a As Double, b As Double, eps As Double) As Boolean
  Return MathAbsD(a - b) <= eps
End Function

Public Function MathDegToRad(deg As Double) As Double
  Return deg * (MATH_PI / 180.0)
End Function

Public Function MathRadToDeg(rad As Double) As Double
  Return rad * (180.0 / MATH_PI)
End Function
