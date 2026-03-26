//@ should_pass
// All struct variants + null covered on Shape? — CS8509 suppressed, no SPIRE009
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Shape
    {
        [Variant] public static partial Shape Circle(double radius);
        [Variant] public static partial Shape Square(int sideLength);
    }

    class Consumer
    {
        int Test(Shape? s) => s switch
        {
            null => 0,
            (Shape.Kind.Circle, double r) => 1,
            (Shape.Kind.Square, int x) => 2,
        };
    }
}
