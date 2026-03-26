//@ should_pass
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct ShapeFCP1
    {
        [Variant] public static partial ShapeFCP1 Dot(double dotX);
        [Variant] public static partial ShapeFCP1 Rect(float rectW, float rectH);
    }
    class CFCP1
    {
        int Test(ShapeFCP1 s) => s switch
        {
            (ShapeFCP1.Kind.Dot, double x) => 1,
            _ => 0,
        };
    }
}
