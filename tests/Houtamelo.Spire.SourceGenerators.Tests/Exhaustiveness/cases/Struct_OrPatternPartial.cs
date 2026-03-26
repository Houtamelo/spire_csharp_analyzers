//@ should_fail
// Or pattern covers two variants but third is missing — SPIRE009
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
            (Shape.Kind.Circle or Shape.Kind.Rectangle, var _) => 1,
        };
    }
}
