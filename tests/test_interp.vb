' String Interpolation ($"...") and String type alias test suite.

Include "std/strings.vb"
Include "harness.vb"

Structure OrderItem
  Dim name As String
  Dim qty As Integer
  Dim price As Double
End Structure

Enum Priority
  Low = 1
  High = 9
End Enum

Private Function FormatTag(label As String, count As Integer) As String
  Return $"[{ label }:{ count }]"
End Function

Private Function PickStr(flag As Boolean, a, b As String) As String
  If flag Then
    Return a
  End If
  Return b
End Function

Test "String type alias and VB.NET-style basic interpolation"
  Dim quantity As Integer = 5
  Dim item_names As String = "apples"
  Dim result As String = $"{ quantity } x { item_names }"
  Assert StrEq(result, "5 x apples"), "expected '5 x apples'"
  Assert SizeOf(String) = SizeOf(Pointer)

  Dim tags(2) As String
  tags(0) = FormatTag("todo", 3)
  tags(1) = FormatTag("done", 7)
  Assert StrEq(tags(0), "[todo:3]")
  Assert StrEq(tags(1), "[done:7]")
End Test

Test "All scalar types, Boolean, Enum, Byte array, and null String"
  Dim b As Byte = 12
  Dim s As Short = -340
  Dim i As Integer = 42
  Dim lBase As Long = 3000000
  Dim l As Long = lBase * 3000
  Dim f As Single = 2.5
  Dim d As Double = 3.25
  Dim t As Boolean = True
  Dim fl As Boolean = False
  Dim p As Integer = Priority.High
  Dim buf(8) As Byte
  buf(0) = 86
  buf(1) = 66
  buf(2) = 0
  Dim nilStr As String = 0

  Dim out1 As String = $"b={b} s={s} i={i} l={l}"
  Assert StrEq(out1, "b=12 s=-340 i=42 l=9000000000"), out1

  Dim out2 As String = $"f={f} d={d} t={t} fl={fl} p={p}"
  Assert StrEq(out2, "f=2.5 d=3.25 t=True fl=False p=9"), out2

  Dim out3 As String = $"arr={buf} nil=[{nilStr}]"
  Assert StrEq(out3, "arr=VB nil=[]"), out3
End Test

Test "Complex expressions, field access, array indexing, and function calls"
  Dim item As OrderItem With ( name = "widgets", qty = 4, price = 12.5 )
  Dim nums(3) As Integer = (10, 20, 30)
  Dim msg As String = $"{item.qty} {item.name} = {item.qty * 10 + nums(1)} ({PickStr(item.qty > 2, "bulk", "single")})"
  Assert StrEq(msg, "4 widgets = 60 (bulk)"), msg
End Test

Test "Escaped braces, percent signs, C escapes, empty & constant folding"
  Dim pct As Integer = 100
  Dim s1 As String = $"{{ {pct}% done }}"
  Assert StrEq(s1, "{ 100% done }"), s1

  Dim sEmpty As String = $""
  Assert StrEq(sEmpty, "")

  Dim sPlain As String = $"plain text {{ok}} 50%"
  Assert StrEq(sPlain, "plain text {ok} 50%"), sPlain

  Dim sEsc As String = $"a\tb={pct}\n"
  Assert StrLen(sEsc) = 8
  Assert sEsc(1) = 9
  Assert sEsc(7) = 10
End Test

Test "Nested interpolation and concatenation with &"
  Dim x As Integer = 7
  Dim nested As String = $"outer=[{$"inner={x * 2}"}]"
  Assert StrEq(nested, "outer=[inner=14]"), nested

  Dim combined As String = $"val={x}" & "!" & $" next={x + 1}"
  Assert StrEq(combined, "val=7! next=8"), combined
End Test

Test "Type Analyzer rejects unsupported struct type in interpolation"
  Dim tmpSrc(256) As Byte
  Dim pid As Integer = getpid()
  If IsWindowsHost() Then
    snprintf(tmpSrc, 256, "%s\\vb_interp_err_%d.vb", GetTempDir(), pid)
  Else
    snprintf(tmpSrc, 256, "%s/vb_interp_err_%d.vb", GetTempDir(), pid)
  End If
  WriteTextFile(tmpSrc, "Structure Pt\n  Dim x As Integer\nEnd Structure\nPublic Function main() As Integer\n  Dim p As Pt With ( x = 1 )\n  Dim s As String = $\"p={p}\"\n  Return 0\nEnd Function\n")
  Dim rc As Integer = RunVb(tmpSrc, "--ta --emit-only")
  unlink(tmpSrc)
  Assert rc <> 0, "expected --ta to reject struct in string interpolation"
  Assert OutHas("Unsupported type in string interpolation")
End Test
