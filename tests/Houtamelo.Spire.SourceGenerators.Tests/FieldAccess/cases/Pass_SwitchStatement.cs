//@ should_pass
// Correct field access in switch statement
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
            switch (s)
            {
                case { kind: Shape.Kind.Circle, circle: { radius: var r } }:
                    return r;
                default:
                    return 0;
            }
        }
    }
}
