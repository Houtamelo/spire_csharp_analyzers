//@ should_fail
// SPIRE014: kind guard is on a different variable — s.radius is still unguarded
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Figure
    {
        [Variant] public static partial Figure Ellipse(double ellipseRadius);
        [Variant] public static partial Figure Rect(int rectWidth);
    }
    class C
    {
        double Test(Figure s, Figure other)
        {
            if (other.kind == Figure.Kind.Ellipse)
            {
                return s.ellipseRadius; //~ ERROR
            }
            return 0;
        }
    }
}
