//@ should_fail
// Shape? with not-null arm covering all variants but null is NOT covered
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
        int Test(Shape? s) => s switch //~ ERROR
        {
            not null and (Shape.Kind.Circle, double r) => 1,
            not null and (Shape.Kind.Square, int x) => 2,
        };
    }
}
