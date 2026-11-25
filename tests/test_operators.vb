' VerBose-Lang Test Suite: Operator Overloading over Structures

Include "harness.vb"

Structure Vec3
  Dim x, y, z As Single
End Structure

Structure Vec2i
  Dim x, y As Integer
End Structure

Structure MaskBox
  Dim bits As Integer
End Structure

Public Operator +(a, b As Vec3) As Vec3
  Return Vec3(a.x + b.x, a.y + b.y, a.z + b.z)
End Operator

Public Operator -(a, b As Vec3) As Vec3
  Return Vec3(a.x - b.x, a.y - b.y, a.z - b.z)
End Operator

Public Operator -(v As Vec3) As Vec3
  Return Vec3(-v.x, -v.y, -v.z)
End Operator

Public Operator *(v As Vec3, s As Single) As Vec3
  Return Vec3(v.x * s, v.y * s, v.z * s)
End Operator

Public Operator *(s As Single, v As Vec3) As Vec3
  Return Vec3(v.x * s, v.y * s, v.z * s)
End Operator

Public Operator *(a, b As Vec3) As Vec3
  Return Vec3(a.x * b.x, a.y * b.y, a.z * b.z)
End Operator

Public Operator /(v As Vec3, s As Single) As Vec3
  Return Vec3(v.x / s, v.y / s, v.z / s)
End Operator

Public Operator =(a, b As Vec3) As Boolean
  Return (a.x = b.x) And (a.y = b.y) And (a.z = b.z)
End Operator

Public Operator <>(a, b As Vec3) As Boolean
  Return Not (a = b)
End Operator

Public Operator +(a, b As Vec2i) As Vec2i
  Return Vec2i(a.x + b.x, a.y + b.y)
End Operator

Public Operator *(v As Vec2i, k As Integer) As Vec2i
  Return Vec2i(v.x * k, v.y * k)
End Operator

Public Operator <(a, b As Vec2i) As Boolean
  Return (a.x + a.y) < (b.x + b.y)
End Operator

Private Operator Not(m As MaskBox) As MaskBox
  Return MaskBox(Not m.bits)
End Operator

Private Operator And(a, b As MaskBox) As MaskBox
  Return MaskBox(a.bits And b.bits)
End Operator

Test "Vec3 binary +, -, *, / and unary -"
  Dim a As Vec3 = Vec3(1.0, 2.0, 3.0)
  Dim b As Vec3 = Vec3(4.0, 5.0, 6.0)
  Dim sum As Vec3 = a + b
  Assert (sum.x = 5.0) And (sum.y = 7.0) And (sum.z = 9.0), "Vec3 +"

  Dim diff As Vec3 = b - a
  Assert (diff.x = 3.0) And (diff.y = 3.0) And (diff.z = 3.0), "Vec3 -"

  Dim negA As Vec3 = -a
  Assert (negA.x = -1.0) And (negA.y = -2.0) And (negA.z = -3.0), "Vec3 unary -"

  Dim prod As Vec3 = a * b
  Assert (prod.x = 4.0) And (prod.y = 10.0) And (prod.z = 18.0), "Vec3 * Vec3"

  Dim half As Vec3 = b / 2.0
  Assert (half.x = 2.0) And (half.y = 2.5) And (half.z = 3.0), "Vec3 / float literal"
End Test

Test "Mixed Struct and Scalar overloads with contextual float literals"
  Dim v As Vec3 = Vec3(2.0, -3.0, 4.0)
  Dim s As Single = 3.0
  Dim r1 As Vec3 = v * s
  Dim r2 As Vec3 = s * v
  Dim r3 As Vec3 = v * 2.0
  Dim r4 As Vec3 = 0.5 * v
  Assert (r1.x = 6.0) And (r1.y = -9.0) And (r1.z = 12.0), "Vec3 * Single"
  Assert (r2.x = 6.0) And (r2.y = -9.0) And (r2.z = 12.0), "Single * Vec3"
  Assert (r3.x = 4.0) And (r3.y = -6.0) And (r3.z = 8.0), "Vec3 * float lit"
  Assert (r4.x = 1.0) And (r4.y = -1.5) And (r4.z = 2.0), "float lit * Vec3"
End Test

Test "Operator precedence, chaining, equality, and rvalue field access"
  Dim a As Vec3 = Vec3(1.0, 2.0, 3.0)
  Dim b As Vec3 = Vec3(2.0, 3.0, 4.0)
  Dim c As Vec3 = a + b * 2.0
  Dim expected As Vec3 = Vec3(5.0, 8.0, 11.0)
  Assert c = expected, "a + b * 2.0 should obey * precedence over +"
  Assert Not (c <> expected), "Vec3 <> should be False when equal"
  Assert a <> b, "Vec3 <> should be True when different"
  Assert (-(a + b)).z = -7.0, "Rvalue field access on chained operator result"
End Test

Test "Multiple struct types and Private unary Not / bitwise And overloads"
  Dim p As Vec2i = Vec2i(3, 4)
  Dim q As Vec2i = Vec2i(1, 2)
  Dim r As Vec2i = p + q * 3
  Assert (r.x = 6) And (r.y = 10), "Vec2i + and *"
  Assert q < p, "Vec2i <"
  Assert Not (p < q), "Not (p < q)"

  Dim m1 As MaskBox = MaskBox(0x0F)
  Dim m2 As MaskBox = MaskBox(0x33)
  Dim mAnd As MaskBox = m1 And m2
  Assert mAnd.bits = 0x03, "MaskBox And"
  Dim mInv As MaskBox = Not m1
  Assert (mInv.bits And 0xFF) = 0xF0, "MaskBox unary Not"
End Test

Test "Type Analyzer validates Operator overloading rules"
  Dim p1(256), p2(256), p3(256), p4(256) As Byte
  Dim pid As Integer = getpid()
  snprintf(p1, 256, "%s/vb_op_nostruct_%d.vb", GetTempDir(), pid)
  snprintf(p2, 256, "%s/vb_op_byref_%d.vb", GetTempDir(), pid)
  snprintf(p3, 256, "%s/vb_op_dup_%d.vb", GetTempDir(), pid)
  snprintf(p4, 256, "%s/vb_op_missing_%d.vb", GetTempDir(), pid)

  Assert WriteTextFile(p1, "Public Operator +(a, b As Integer) As Integer\n  Return a + b\nEnd Operator\nPublic Function main() As Integer\n  Return 0\nEnd Function\n"), "write op nostruct"
  Dim rc1 As Integer = RunVb(p1, "")
  unlink(p1)
  Assert rc1 <> 0, "Operator without struct param must fail"
  Assert OutHas("at least one Structure parameter"), "expected struct param error"

  Assert WriteTextFile(p2, "Structure P\n  Dim x As Integer\nEnd Structure\nPublic Operator +(ByRef a As P, b As P) As P\n  Return a\nEnd Operator\nPublic Function main() As Integer\n  Return 0\nEnd Function\n"), "write op byref"
  Dim rc2 As Integer = RunVb(p2, "")
  unlink(p2)
  Assert rc2 <> 0, "Operator with ByRef param must fail"
  Assert OutHas("cannot be ByRef"), "expected ByRef error"

  Assert WriteTextFile(p3, "Structure P\n  Dim x As Integer\nEnd Structure\nPublic Operator +(a, b As P) As P\n  Return P(a.x + b.x)\nEnd Operator\nPublic Operator +(a, b As P) As P\n  Return P(a.x + b.x)\nEnd Operator\nPublic Function main() As Integer\n  Return 0\nEnd Function\n"), "write op dup"
  Dim rc3 As Integer = RunVb(p3, "")
  unlink(p3)
  Assert rc3 <> 0, "Duplicate operator overload must fail"
  Assert OutHas("Duplicate operator overload"), "expected duplicate error"

  Assert WriteTextFile(p4, "Structure P\n  Dim x As Integer\nEnd Structure\nPublic Function main() As Integer\n  Dim a As P = P(1)\n  Dim b As P = P(2)\n  Dim c As P = a + b\n  Return c.x\nEnd Function\n"), "write op missing"
  Dim rc4 As Integer = RunVb(p4, "")
  unlink(p4)
  Assert rc4 <> 0, "Missing struct operator overload must fail"
  Assert OutHas("No matching Operator overload"), "expected missing overload error"
End Test
