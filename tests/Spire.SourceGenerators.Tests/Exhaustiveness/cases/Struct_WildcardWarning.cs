//@ should_fail
// Wildcard covers missing variants — SPIRE010
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Shape
    {
        [Variant] public static partial Shape Circle(double radius);
        [Variant] public static partial Shape Rectangle(float width, float height);
        [Variant] public static partial Shape Square(int sideLength);
    }

    class Consumer
    {
        int Test(Shape s) => s switch //~ ERROR
        {
            (Shape.Kind.Circle, double r) => 1,
            _ => 0,
        };
    }
}
