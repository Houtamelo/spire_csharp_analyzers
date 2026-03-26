//@ should_pass
// Correct field access in switch statement
using Spire;
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
            switch (s)
            {
                case (Shape.Kind.Circle, _):
                    return s.radius;
                default:
                    return 0;
            }
        }
    }
}
