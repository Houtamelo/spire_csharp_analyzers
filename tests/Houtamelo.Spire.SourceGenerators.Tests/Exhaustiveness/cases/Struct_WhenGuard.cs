//@ should_fail
// Circle arm has when guard — not fully covered — SPIRE009
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
            (Shape.Kind.Circle, double r) when r > 0 => 1,
            (Shape.Kind.Rectangle, var w, var h) => 2,
            (Shape.Kind.Square, int x) => 3,
        };
    }
}
