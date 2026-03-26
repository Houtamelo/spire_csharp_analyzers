//@ should_fail
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct ShapePP2
    {
        [Variant] public static partial ShapePP2 Circle(double radius);
        [Variant] public static partial ShapePP2 Square(int sideLength);
    }
    class CPP2
    {
        int Test(ShapePP2 s) => s switch
        {
            { kind: ShapePP2.Kind.Circle, radius: int r } => 1, //~ ERROR
            { kind: ShapePP2.Kind.Square } => 2,
        };
    }
}
