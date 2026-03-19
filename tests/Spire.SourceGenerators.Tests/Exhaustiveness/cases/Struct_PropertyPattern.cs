//@ should_fail
// Property pattern matching on tag — missing Rectangle — SPIRE009
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
            { tag: Shape.Kind.Circle } => 1,
            { tag: Shape.Kind.Square } => 2,
        };
    }
}
