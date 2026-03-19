//@ should_pass
// Or pattern combined with individual arm covers all variants
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
        int Test(Shape s) => s switch
        {
            (Shape.Kind.Circle, double r) => 1,
            (Shape.Kind.Rectangle or Shape.Kind.Square, var _) => 2,
        };
    }
}
