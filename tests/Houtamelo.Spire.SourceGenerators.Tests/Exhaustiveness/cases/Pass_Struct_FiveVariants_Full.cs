//@ should_pass
// 5-variant struct all 5 covered — no diagnostic
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct FivePolygon
    {
        [Variant] public static partial FivePolygon FiveTri(double triSide);
        [Variant] public static partial FivePolygon FiveQuad(double quadWidth, double quadHeight);
        [Variant] public static partial FivePolygon FivePent(double pentSide);
        [Variant] public static partial FivePolygon FiveHex(double hexSide);
        [Variant] public static partial FivePolygon FiveCirc(double circRadius);
    }

    class PassFivePolygonConsumer
    {
        double Perimeter(FivePolygon p) => p switch
        {
            (FivePolygon.Kind.FiveTri, double s) => 3 * s,
            (FivePolygon.Kind.FiveQuad, double w, double h) => 2 * (w + h),
            (FivePolygon.Kind.FivePent, double s) => 5 * s,
            (FivePolygon.Kind.FiveHex, double s) => 6 * s,
            (FivePolygon.Kind.FiveCirc, double r) => 2 * System.Math.PI * r,
        };
    }
}
