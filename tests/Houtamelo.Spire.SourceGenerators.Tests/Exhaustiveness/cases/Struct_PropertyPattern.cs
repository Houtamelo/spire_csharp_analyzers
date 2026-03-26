//@ should_fail
// Property pattern matching on kind — missing Rectangle — SPIRE009
using Houtamelo.Spire;
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
            { kind: Shape.Kind.Circle } => 1,
            { kind: Shape.Kind.Square } => 2,
        };
    }
}
