//@ should_fail
// or pattern covers Circle and Square but not Rectangle
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
    class C
    {
        int Test(Shape s) => s switch //~ ERROR
        {
            (Shape.Kind.Circle, _) or (Shape.Kind.Square, _) => 1,
        };
    }
}
