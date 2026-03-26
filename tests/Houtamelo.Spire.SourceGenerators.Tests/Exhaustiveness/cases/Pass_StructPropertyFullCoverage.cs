//@ should_pass
// Property pattern covering all variants — no diagnostic
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
            { kind: Shape.Kind.Circle } => 1,
            { kind: Shape.Kind.Rectangle } => 2,
            { kind: Shape.Kind.Square } => 3,
        };
    }
}
