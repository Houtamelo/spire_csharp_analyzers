//@ should_pass
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct ShapePPK
    {
        [Variant] public static partial ShapePPK Circle(double radius);
        [Variant] public static partial ShapePPK Square(int sideLength);
    }
    class CPPK
    {
        int Test(ShapePPK s) => s switch
        {
            { kind: ShapePPK.Kind.Circle } => 1,
            { kind: ShapePPK.Kind.Square } => 2,
        };
    }
}
