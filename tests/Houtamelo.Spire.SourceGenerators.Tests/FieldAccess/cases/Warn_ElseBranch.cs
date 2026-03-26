//@ should_fail
// SPIRE014: else branch is NOT considered guarded
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Shape
    {
        [Variant] public static partial Shape Circle(double radius);
        [Variant] public static partial Shape Square(int sideLength);
    }
    class C
    {
        double Test(Shape s)
        {
            if (s.kind == Shape.Kind.Circle)
            {
                return s.radius;
            }
            else
            {
                return s.radius; //~ ERROR
            }
        }
    }
}
