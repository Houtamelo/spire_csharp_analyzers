//@ should_pass
using Houtamelo.Spire;
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
            { kind: ShapePPV.Kind.Circle, circle: { radius: var r } } => 1,
            { kind: ShapePPV.Kind.Square, square: { sideLength: var x } } => 2,
        };
    }
}
