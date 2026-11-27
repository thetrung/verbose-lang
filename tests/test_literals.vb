' VerBose Test Suite: 64-bit Integer Literals, Scientific Float Literals & IEEE-754 Float <>

Static G_BIG_LONG As Long = 9223372036854775807
Static G_NEG_BIG As Long = -1000000000000
Static G_HEX_64 As Long = 0x123456789ABCDEF
Static G_SCI_DBL As Double = 1.7976931348623157e308
Static G_SCI_NEG As Double = -1.0e-4
Static G_SCI_SNG As Single = 2.5e3

Function MakeZeroD() As Double
  Return 0.0
End Function

Function MakeZeroS() As Single
  Return 0.0
End Function

Test "64-bit integer literals (decimal, negative, hex, static, arithmetic)"
  Dim maxI64 As Long = 9223372036854775807
  Dim trillion As Long = 1000000000000
  Dim negTrillion As Long = -1000000000000
  Dim hex64 As Long = 0x100000000
  Dim hexFull As Long = 0x123456789ABCDEF

  Assert maxI64 = G_BIG_LONG, "INT64_MAX literal matches static"
  Assert (maxI64 - 1) = 9223372036854775806, "INT64_MAX - 1"
  Assert trillion > 2147483647, "1 trillion > INT32_MAX"
  Assert (trillion + negTrillion) = 0, "1 trillion + -1 trillion = 0"
  Assert negTrillion = G_NEG_BIG, "negative 64-bit static"
  Assert hex64 = 4294967296, "0x100000000 == 2^32"
  Assert hexFull = G_HEX_64, "64-bit hex literal"

  Dim minI64 As Long = -9223372036854775808
  Assert minI64 < 0, "INT64_MIN < 0"
  Assert (minI64 + 1) = -9223372036854775807, "INT64_MIN + 1"

  Dim small As Integer = 42
  Assert small = 42, "32-bit literal still Integer"
End Test

Test "scientific notation float literals (Double, Single, +/-/unsigned exp, no-dot)"
  Dim a As Double = 1.0e-4
  Dim b As Double = 1e5
  Dim c As Double = 2.5E+3
  Dim d As Double = 1.7976931348623157e308
  Dim e As Double = -3.5e-2
  Dim f As Single = 1.5e2

  Assert (a > 0.000099) And (a < 0.000101), "1.0e-4"
  Assert (b > 99999.5) And (b < 100000.5), "1e5 without dot"
  Assert (c > 2499.5) And (c < 2500.5), "2.5E+3"
  Assert d = G_SCI_DBL, "DBL_MAX scientific literal"
  Assert d > 1.0e300, "DBL_MAX > 1.0e300"
  Assert (e < -0.034) And (e > -0.036), "-3.5e-2"
  Assert (G_SCI_NEG < -0.000099) And (G_SCI_NEG > -0.000101), "static -1.0e-4"
  Assert (Double(f) > 149.5) And (Double(f) < 150.5), "Single 1.5e2"
  Assert (Double(G_SCI_SNG) > 2499.5) And (Double(G_SCI_SNG) < 2500.5), "Static Single 2.5e3"
End Test

Test "IEEE-754 unordered float <> (NaN <> NaN is True)"
  Dim zd As Double = MakeZeroD()
  Dim nanD As Double = zd / zd
  Assert Not (nanD = nanD), "Double NaN = NaN is False"
  Assert nanD <> nanD, "Double NaN <> NaN is True (fcmp une)"
  Assert nanD <> 0.0, "Double NaN <> 0.0 is True"
  Assert Boolean(nanD), "Boolean(Double NaN) is True"
  Assert Not Boolean(0.0), "Boolean(0.0) is False"

  Dim zs As Single = MakeZeroS()
  Dim nanS As Single = zs / zs
  Assert Not (nanS = nanS), "Single NaN = NaN is False"
  Assert nanS <> nanS, "Single NaN <> NaN is True (fcmp une)"
  Assert Boolean(nanS), "Boolean(Single NaN) is True"

  Dim x As Double = 3.5
  Assert Not (x <> 3.5), "3.5 <> 3.5 is False"
  Assert x <> 4.5, "3.5 <> 4.5 is True"
End Test
