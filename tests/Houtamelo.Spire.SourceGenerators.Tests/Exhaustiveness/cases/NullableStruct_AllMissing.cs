//@ should_fail
// Shape? switch with only null arm — all variants missing
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Shape
    {
        [Variant] public static partial Shape Circle(double radius);
        [Variant] public static partial Shape Square(int sideLength);
    }

    class Consumer
    {
        int Test(Shape? s) => s switch //~ ERROR
        {
            null => 0,
        };
    }
}
