//@ should_pass
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
            (Shape.Kind.Circle, double _) => 1,
            (Shape.Kind.Square, int _) => 2,
        };
    }
}
