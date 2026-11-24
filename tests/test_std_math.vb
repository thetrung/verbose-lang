' Pure VerBose unit tests for std/math.vb (zero C -lm dependency).

Include "../std/math.vb"

Test "Math constants and MathNearlyEq"
  Assert MathNearlyEq(MATH_TAU, 2.0 * MATH_PI, MATH_EPS)
  Assert MathNearlyEq(MATH_HALF_PI, MATH_PI * 0.5, MATH_EPS)
  Assert MathNearlyEq(MATH_E, 2.718281828459045, MATH_EPS)
  Assert MathNearlyEq(1.0, 1.0000000000001, MATH_EPS)
  Assert Not MathNearlyEq(1.0, 1.001, MATH_EPS)
End Test

Test "Integer math: Abs, Min, Max, Clamp, Sign"
  Assert MathAbsI(0) = 0
  Assert MathAbsI(42) = 42
  Assert MathAbsI(-42) = 42

  Assert MathMinI(3, 7) = 3
  Assert MathMinI(-5, -10) = -10
  Assert MathMaxI(3, 7) = 7
  Assert MathMaxI(-5, -10) = -5

  Assert MathClampI(5, 1, 10) = 5
  Assert MathClampI(-3, 1, 10) = 1
  Assert MathClampI(99, 1, 10) = 10

  Assert MathSignI(-100) = -1
  Assert MathSignI(0) = 0
  Assert MathSignI(100) = 1
End Test

Test "Integer math: Gcd, Lcm, PowI, Isqrt"
  Assert MathGcd(0, 0) = 0
  Assert MathGcd(0, 15) = 15
  Assert MathGcd(15, 0) = 15
  Assert MathGcd(48, 18) = 6
  Assert MathGcd(-48, 18) = 6
  Assert MathGcd(17, 13) = 1

  Assert MathLcm(0, 5) = 0
  Assert MathLcm(5, 0) = 0
  Assert MathLcm(12, 18) = 36
  Assert MathLcm(-6, 8) = 24

  Assert MathPowI(2, 0) = 1
  Assert MathPowI(2, 10) = 1024
  Assert MathPowI(-3, 3) = -27
  Assert MathPowI(-3, 4) = 81
  Assert MathPowI(5, -2) = 0

  Assert MathIsqrt(-5) = 0
  Assert MathIsqrt(0) = 0
  Assert MathIsqrt(1) = 1
  Assert MathIsqrt(2) = 1
  Assert MathIsqrt(3) = 1
  Assert MathIsqrt(4) = 2
  Assert MathIsqrt(15) = 3
  Assert MathIsqrt(16) = 4
  Assert MathIsqrt(1000000) = 1000
  Assert MathIsqrt(2147483647) = 46340
End Test

Test "Long math: AbsL, MinL, MaxL, ClampL"
  Dim billion As Long = 1000000000
  Dim bigPos As Long = billion * 5
  Dim bigNeg As Long = -bigPos
  Dim biggerPos As Long = billion * 9
  Dim biggerNeg As Long = -biggerPos
  Assert MathAbsL(bigNeg) = bigPos
  Assert MathAbsL(0) = 0
  Assert MathMinL(bigNeg, bigPos) = bigNeg
  Assert MathMaxL(bigNeg, bigPos) = bigPos
  Assert MathClampL(0, bigNeg, bigPos) = 0
  Assert MathClampL(biggerNeg, bigNeg, bigPos) = bigNeg
  Assert MathClampL(biggerPos, bigNeg, bigPos) = bigPos
End Test

Test "Double math: AbsD, MinD, MaxD, ClampD, Lerp, Deg/Rad"
  Assert MathNearlyEq(MathAbsD(-3.25), 3.25, MATH_EPS)
  Assert MathNearlyEq(MathAbsD(3.25), 3.25, MATH_EPS)
  Assert MathNearlyEq(MathMinD(-1.5, 2.5), -1.5, MATH_EPS)
  Assert MathNearlyEq(MathMaxD(-1.5, 2.5), 2.5, MATH_EPS)
  Assert MathNearlyEq(MathClampD(0.5, 0.0, 1.0), 0.5, MATH_EPS)
  Assert MathNearlyEq(MathClampD(-0.5, 0.0, 1.0), 0.0, MATH_EPS)
  Assert MathNearlyEq(MathClampD(1.5, 0.0, 1.0), 1.0, MATH_EPS)

  Assert MathNearlyEq(MathLerp(10.0, 20.0, 0.0), 10.0, MATH_EPS)
  Assert MathNearlyEq(MathLerp(10.0, 20.0, 0.25), 12.5, MATH_EPS)
  Assert MathNearlyEq(MathLerp(10.0, 20.0, 1.0), 20.0, MATH_EPS)

  Assert MathNearlyEq(MathDegToRad(180.0), MATH_PI, MATH_EPS)
  Assert MathNearlyEq(MathDegToRad(90.0), MATH_HALF_PI, MATH_EPS)
  Assert MathNearlyEq(MathRadToDeg(MATH_PI), 180.0, MATH_EPS)
  Assert MathNearlyEq(MathRadToDeg(MATH_TAU), 360.0, MATH_EPS)
End Test

Test "Double rounding: MathFloor, MathCeil, MathRound (positive, negative, exact)"
  Assert MathFloor(2.7) = 2.0
  Assert MathFloor(2.0) = 2.0
  Assert MathFloor(0.0) = 0.0
  Assert MathFloor(-2.3) = -3.0
  Assert MathFloor(-2.0) = -2.0

  Assert MathCeil(2.3) = 3.0
  Assert MathCeil(2.0) = 2.0
  Assert MathCeil(0.0) = 0.0
  Assert MathCeil(-2.7) = -2.0
  Assert MathCeil(-2.0) = -2.0

  Assert MathRound(2.4) = 2.0
  Assert MathRound(2.5) = 3.0
  Assert MathRound(0.0) = 0.0
  Assert MathRound(-2.4) = -2.0
  Assert MathRound(-2.5) = -3.0
End Test

Test "MathSqrt across scales and edge cases"
  Assert MathSqrt(-4.0) = 0.0
  Assert MathSqrt(0.0) = 0.0
  Assert MathNearlyEq(MathSqrt(1.0), 1.0, MATH_EPS)
  Assert MathNearlyEq(MathSqrt(2.0), 1.4142135623730951, MATH_EPS)
  Assert MathNearlyEq(MathSqrt(9.0), 3.0, MATH_EPS)
  Assert MathNearlyEq(MathSqrt(0.25), 0.5, MATH_EPS)
  Assert MathNearlyEq(MathSqrt(0.0001), 0.01, MATH_EPS)
  Assert MathNearlyEq(MathSqrt(100000000.0), 10000.0, MATH_EPS)
End Test

Test "Trig: MathSin, MathCos, MathTan and range reduction"
  Dim tol As Double = 0.0000000001
  Assert MathNearlyEq(MathSin(0.0), 0.0, tol)
  Assert MathNearlyEq(MathSin(MATH_HALF_PI), 1.0, tol)
  Assert MathNearlyEq(MathSin(MATH_PI), 0.0, tol)
  Assert MathNearlyEq(MathSin(-MATH_HALF_PI), -1.0, tol)
  Assert MathNearlyEq(MathSin(MATH_PI / 6.0), 0.5, tol)

  Assert MathNearlyEq(MathCos(0.0), 1.0, tol)
  Assert MathNearlyEq(MathCos(MATH_HALF_PI), 0.0, tol)
  Assert MathNearlyEq(MathCos(MATH_PI), -1.0, tol)
  Assert MathNearlyEq(MathCos(MATH_PI / 3.0), 0.5, tol)

  ' Large angle range reduction & Pythagorean identity
  Dim bigAngle As Double = 100.0 * MATH_PI + MATH_PI / 4.0
  Dim s As Double = MathSin(bigAngle)
  Dim c As Double = MathCos(bigAngle)
  Assert MathNearlyEq(s * s + c * c, 1.0, tol)
  Assert MathNearlyEq(MathTan(0.0), 0.0, tol)
  Assert MathNearlyEq(MathTan(MATH_PI / 4.0), 1.0, tol)
  Assert MathNearlyEq(MathTan(-MATH_PI / 4.0), -1.0, tol)
End Test

Test "MathExp, MathLn, MathPow"
  Dim tol As Double = 0.0000000001
  Assert MathNearlyEq(MathExp(0.0), 1.0, tol)
  Assert MathNearlyEq(MathExp(1.0), MATH_E, tol)
  Assert MathNearlyEq(MathExp(-1.0), 1.0 / MATH_E, tol)
  Assert MathNearlyEq(MathExp(5.0), 148.4131591025766, 0.00000001)

  Assert MathLn(-1.0) = 0.0
  Assert MathLn(0.0) = 0.0
  Assert MathNearlyEq(MathLn(1.0), 0.0, tol)
  Assert MathNearlyEq(MathLn(MATH_E), 1.0, tol)
  Assert MathNearlyEq(MathLn(2.0), 0.6931471805599453, tol)
  Assert MathNearlyEq(MathLn(MathExp(3.5)), 3.5, tol)
  Assert MathNearlyEq(MathExp(MathLn(12.345)), 12.345, 0.00000001)

  Assert MathNearlyEq(MathPow(2.0, 0.0), 1.0, tol)
  Assert MathNearlyEq(MathPow(2.0, 10.0), 1024.0, 0.0000001)
  Assert MathNearlyEq(MathPow(9.0, 0.5), 3.0, tol)
  Assert MathNearlyEq(MathPow(2.0, -3.0), 0.125, tol)
  Assert MathNearlyEq(MathPow(-2.0, 3.0), -8.0, tol)
  Assert MathNearlyEq(MathPow(-2.0, 4.0), 16.0, tol)
  Assert MathPow(-2.0, 0.5) = 0.0
End Test
