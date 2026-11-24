Declare Function sqrt(x As Double) As Double
Declare Function sinf(x As Single) As Single
Declare Function cos(x As Double) As Double
Declare Function pow(base As Double, exp As Double) As Double

Public Function main() As Integer
  Dim r As Double = sqrt(16.0)
  Dim p As Double = pow(2.0, 10.0)
  Dim s As Single = sinf(0.0)
  Dim c As Double = cos(0.0)
  Dim neg_r As Double = -r
  Dim n As Integer = 42
  Dim neg_n As Integer = -n
  printf("sqrt=%.1f pow=%.1f sinf=%.1f cos=%.1f neg=%.1f,%d\n", r, p, s, c, neg_r, neg_n)
  Return 0
End Function
