' Built-in Primitive Conversion Tests: Single(x), Integer(x), Double(x), Long(x), Short(x), Byte(x), Boolean(x), Pointer(x)

Public Enum CastColor
  CastRed = 1
  CastGreen = 2
  CastBlue = 3
End Enum

Private Function NearF(a, b As Single) As Boolean
  Dim d As Single = a - b
  If d < 0.0 Then
    d = -d
  End If
  Return d < 0.0005
End Function

Private Function NearD(a, b As Double) As Boolean
  Dim d As Double = a - b
  If d < 0.0 Then
    d = -d
  End If
  Return d < 0.00001
End Function

Test "Integer <-> Single and Double conversions"
  Dim i As Integer = 42
  Dim f As Single = Single(i)
  Assert NearF(f, 42.0), "Single(Integer)"

  Dim neg As Integer = -17
  Dim nf As Single = Single(neg)
  Assert NearF(nf, -17.0), "Single(negative Integer)"

  Dim f2 As Single = 3.75
  Assert Integer(f2) = 3, "Integer(Single) truncates toward zero"
  Dim f3 As Single = -9.9
  Assert Integer(f3) = -9, "Integer(negative Single)"

  Dim d As Double = Double(i) + 0.5
  Assert NearD(d, 42.5), "Double(Integer)"
  Assert NearF(Single(d), 42.5), "Single(Double)"
  Assert Integer(d) = 42, "Integer(Double)"

  ' Mixed expression using explicit Single(x)
  Dim w As Integer = 1280
  Dim h As Integer = 720
  Dim aspect As Single = Single(w) / Single(h)
  Assert NearF(aspect, 1.7777778), "Single(w) / Single(h)"
End Test

Test "Byte, Short, Long, and Enum conversions"
  Dim big As Long = Long(100000) * Long(100000)
  Assert (big / Long(100000)) = Long(100000), "Long(Integer) avoids 32-bit overflow"
  Assert big > Long(2000000000), "Long(100000)*Long(100000) exceeds i32 max"
  Assert Integer(Long(12345)) = 12345, "Integer(Long)"

  Dim sh As Short = Short(300)
  Assert Integer(sh) = 300, "Integer(Short)"

  Dim b As Byte = Byte(65)
  Assert Integer(b) = 65, "Byte(65)"

  ' Float to Byte for values > 127 (RGB channel safe: goes via i32 -> trunc i8)
  Dim rgb As Byte = Byte(255.0)
  Assert (Integer(rgb) And 255) = 255, "Byte(255.0) preserves 0xFF bit pattern"
  Dim mid As Byte = Byte(200.4)
  Assert (Integer(mid) And 255) = 200, "Byte(200.4) preserves 200"

  Dim c As CastColor = CastBlue
  Assert Integer(c) = 3, "Integer(Enum)"
  Assert NearF(Single(c), 3.0), "Single(Enum)"
End Test

Test "Boolean and Pointer conversions"
  Assert Boolean(1) = True, "Boolean(1)"
  Assert Boolean(0) = False, "Boolean(0)"
  Assert Boolean(-5) = True, "Boolean(-5)"
  Assert Boolean(0.0) = False, "Boolean(0.0)"
  Assert Boolean(0.25) = True, "Boolean(0.25)"

  Assert Integer(True) = 1, "Integer(True)"
  Assert Integer(False) = 0, "Integer(False)"
  Assert NearF(Single(True), 1.0), "Single(True)"
  Assert NearF(Single(False), 0.0), "Single(False)"

  Dim x As Integer = 99
  Dim p As Pointer = AddressOf(x)
  Dim addr As Long = Long(p)
  Assert addr <> 0, "Long(Pointer) non-zero"
  Assert Boolean(p) = True, "Boolean(non-null Pointer)"
  Dim p2 As Pointer = Pointer(addr)
  Assert PtrRead(p2, Integer) = 99, "Pointer(Long(AddressOf(x))) round-trip"
  Dim nullP As Pointer = Pointer(0)
  Assert Boolean(nullP) = False, "Boolean(Pointer(0))"
End Test
