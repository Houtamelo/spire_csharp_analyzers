//@ should_fail
// Shape? switch covers all variants but not null
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
        int Test(Shape? s) => s switch //~ ERROR
        {
            (Shape.Kind.Circle, double r) => 1,
            (Shape.Kind.Square, int x) => 2,
        };
    }
}
