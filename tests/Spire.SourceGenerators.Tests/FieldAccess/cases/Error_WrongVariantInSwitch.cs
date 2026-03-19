//@ should_fail
// SPIRE013: accessing Square's field inside Circle arm
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
        double Test(Shape s) => s switch
        {
            (Shape.Kind.Circle, _) => s.square_sideLength, //~ ERROR
            _ => 0,
        };
    }
}
