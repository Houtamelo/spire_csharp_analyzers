//@ should_fail
// SPIRE013: 3-variant union — accessing Triangle's field inside Circle arm
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Poly
    {
        [Variant] public static partial Poly Circle(double radius);
        [Variant] public static partial Poly Square(int sideLength);
        [Variant] public static partial Poly Triangle(float base_);
    }
    class C
    {
        double Test(Poly p) => p switch
        {
            (Poly.Kind.Circle, _) => p.base_, //~ ERROR
            _ => 0,
        };
    }
}
