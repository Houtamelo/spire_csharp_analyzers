//@ should_pass
// Correct explicit type in property pattern is not flagged — only wrong types trigger SPIRE011
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct ShapePP1
    {
        [Variant] public static partial ShapePP1 Circle(double radius);
        [Variant] public static partial ShapePP1 Square(int sideLength);
    }
    class CPP1
    {
        int Test(ShapePP1 s) => s switch
        {
            { kind: ShapePP1.Kind.Circle, radius: double r } => 1,
            { kind: ShapePP1.Kind.Square } => 2,
        };
    }
}
