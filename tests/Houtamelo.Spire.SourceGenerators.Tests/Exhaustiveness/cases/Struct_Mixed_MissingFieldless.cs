//@ should_fail
// Mix of fielded and fieldless variants, missing fieldless None — SPIRE009
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct MaybeShape
    {
        [Variant] public static partial MaybeShape Circle(double circleRadius);
        [Variant] public static partial MaybeShape Square(int squareSide);
        [Variant] public static partial MaybeShape None();
    }

    class MaybeShapeConsumer
    {
        double Area(MaybeShape s) => s switch //~ ERROR
        {
            (MaybeShape.Kind.Circle, double r) => System.Math.PI * r * r,
            (MaybeShape.Kind.Square, int side) => side * side,
        };
    }
}
