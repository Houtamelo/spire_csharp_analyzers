//@ should_fail
// SPIRE014: variant field in string interpolation with no kind guard
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Color
    {
        [Variant] public static partial Color Rgb(int rgbValue);
        [Variant] public static partial Color Named(string colorName);
    }
    class C
    {
        string Test(Color c) => $"color={c.colorName}"; //~ ERROR
    }
}
