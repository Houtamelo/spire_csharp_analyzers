//@ should_pass
// Shape? with all variants + var covers null — no diagnostic
using Houtamelo.Spire;
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
            (Shape.Kind.Circle, double r) => 1,
            (Shape.Kind.Square, int x) => 2,
            var other => 0,
        };
    }
}
