//@ should_fail
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct ColorUnion
    {
        [Variant] public static partial ColorUnion Gray(byte luminance);
        [Variant] public static partial ColorUnion Rgb(double red, double green, double blue);
    }
    class CThree
    {
        int Test(ColorUnion c) => c switch
        {
            { kind: ColorUnion.Kind.Rgb, rgb: (double r, string bad, double b) } => 1, //~ ERROR
            _ => 0,
        };
    }
}
