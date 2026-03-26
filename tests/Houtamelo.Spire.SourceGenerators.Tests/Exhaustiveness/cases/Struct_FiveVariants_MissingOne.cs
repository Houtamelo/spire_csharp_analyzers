//@ should_fail
// 5-variant struct, switch covers 4, missing Pentagon — SPIRE009
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Polygon
    {
        [Variant] public static partial Polygon Triangle(double triBase, double triHeight);
        [Variant] public static partial Polygon Quadrilateral(double quadWidth, double quadHeight);
        [Variant] public static partial Polygon Pentagon(double pentSide);
        [Variant] public static partial Polygon Hexagon(double hexSide);
        [Variant] public static partial Polygon Circle(double circRadius);
    }

    class PolygonConsumer
    {
        double Perimeter(Polygon p) => p switch //~ ERROR
        {
            (Polygon.Kind.Triangle, double b, double h) => b + 2 * h,
            (Polygon.Kind.Quadrilateral, double w, double h) => 2 * (w + h),
            (Polygon.Kind.Hexagon, double s) => 6 * s,
            (Polygon.Kind.Circle, double r) => 2 * System.Math.PI * r,
        };
    }
}
