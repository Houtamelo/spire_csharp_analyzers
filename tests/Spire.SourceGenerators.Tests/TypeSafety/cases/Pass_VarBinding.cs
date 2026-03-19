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
            (Shape.Kind.Circle, var r) => 1,
            (Shape.Kind.Square, var x) => 2,
        };
    }
}
