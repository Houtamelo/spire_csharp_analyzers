//@ should_pass
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct ShapePPV
    {
        [Variant] public static partial ShapePPV Circle(double radius);
        [Variant] public static partial ShapePPV Square(int sideLength);
    }
    class CPPV
    {
        int Test(ShapePPV s) => s switch
        {
            { kind: ShapePPV.Kind.Circle, radius: var r } => 1,
            { kind: ShapePPV.Kind.Square, sideLength: var x } => 2,
        };
    }
}
