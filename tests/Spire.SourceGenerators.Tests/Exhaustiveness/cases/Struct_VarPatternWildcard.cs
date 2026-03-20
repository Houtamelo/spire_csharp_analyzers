//@ should_pass
// var pattern is a wildcard catch-all — no diagnostic (refactoring only)
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
        int Test(Shape s) => s switch
        {
            var x => 0,
        };
    }
}
