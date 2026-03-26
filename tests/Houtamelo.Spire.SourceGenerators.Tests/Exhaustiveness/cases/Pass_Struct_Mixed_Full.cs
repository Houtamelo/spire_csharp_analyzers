//@ should_pass
// Mix of fielded and fieldless variants all covered — no diagnostic
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct MixedShape
    {
        [Variant] public static partial MixedShape MixedCircle(double mixedRadius);
        [Variant] public static partial MixedShape MixedSquare(int mixedSide);
        [Variant] public static partial MixedShape MixedNone();
    }

    class PassMixedShapeConsumer
    {
        double Area(MixedShape s) => s switch
        {
            (MixedShape.Kind.MixedCircle, double r) => System.Math.PI * r * r,
            (MixedShape.Kind.MixedSquare, int side) => side * side,
            (MixedShape.Kind.MixedNone, _) => 0.0,
        };
    }
}
