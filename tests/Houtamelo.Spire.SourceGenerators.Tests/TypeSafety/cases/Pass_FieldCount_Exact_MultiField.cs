//@ should_pass
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct ColorFCP2
    {
        [Variant] public static partial ColorFCP2 Gray(byte grayLum);
        [Variant] public static partial ColorFCP2 Rgb(double redCh, double greenCh, double blueCh);
    }
    class CFCP2
    {
        int Test(ColorFCP2 c) => c switch
        {
            (ColorFCP2.Kind.Rgb, double r, double g, double b) => 1,
            _ => 0,
        };
    }
}
