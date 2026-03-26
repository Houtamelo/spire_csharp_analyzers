//@ should_pass
// CS8509 suppressed — all struct variants covered
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
            (Shape.Kind.Circle, double r) => 1,
            (Shape.Kind.Square, int x) => 2,
        };
    }
}
