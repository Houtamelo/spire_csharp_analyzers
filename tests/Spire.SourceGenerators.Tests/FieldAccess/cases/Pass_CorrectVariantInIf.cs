//@ should_pass
// Accessing Circle's field inside kind==Circle guard — OK
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
            if (s.kind == Shape.Kind.Circle)
            {
                return s.radius;
            }
            return 0;
        }
    }
}
