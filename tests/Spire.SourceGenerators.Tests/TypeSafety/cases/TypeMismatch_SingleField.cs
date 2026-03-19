//@ should_fail
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Shape
    {
        [Variant] public static partial Shape Circle(double radius);
        [Variant] public static partial Shape Square(int sideLength);
    }
    class C
    {
        int Test(Shape s) => s switch
        {
            (Shape.Kind.Circle, string bad) => 1, //~ ERROR
            (Shape.Kind.Square, int x) => 2,
        };
    }
}
