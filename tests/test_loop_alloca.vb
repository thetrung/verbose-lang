' Regression test for entry-block alloca hoisting (v0.7.2):
' Loops declaring large stack arrays and returning structs must execute in constant O(1) stack space.

Structure Vec3Test
  Dim x, y, z As Single
End Structure

Private Function AddVec3(a, b As Vec3Test) As Vec3Test
  Return Vec3Test(a.x + b.x, a.y + b.y, a.z + b.z)
End Function

Test "Hoisted loop allocas use constant stack space"
  Dim acc As Long = 0
  Dim v As Vec3Test = Vec3Test(1.0, 2.0, 3.0)
  ' 200,000 iterations * 1,024 bytes = 200 MB if unhoisted (would instantly overflow 8 MB stack!)
  Dim i As Integer = 0
  While i < 200000 Do
    Dim scratch(1024) As Byte
    scratch(0) = Byte(i And 255)
    scratch(1023) = Byte((i + 7) And 255)
    Dim dv As Vec3Test = AddVec3(v, Vec3Test(0.5, 0.25, 0.125))
    acc = acc + Long(Integer(scratch(0)) And 255) + Long(Integer(dv.x))
    i = i + 1
  End While
  Assert acc > Long(0)
End Test

Test "Loop-local uninitialized Dim re-zeros each iteration"
  Dim sum As Integer = 0
  For k = 0 To 9 Do
    Dim x As Integer
    Assert x = 0
    x = k + 1
    sum = sum + x
  End For
  Assert sum = 55
End Test
