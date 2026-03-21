//@ should_pass
// Field access inside nested if where OUTER if has tag guard
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
            if (s.tag == Shape.Kind.Circle)
            {
                if (true)
                {
                    return s.radius;
                }
            }
            return 0;
        }
    }
}
