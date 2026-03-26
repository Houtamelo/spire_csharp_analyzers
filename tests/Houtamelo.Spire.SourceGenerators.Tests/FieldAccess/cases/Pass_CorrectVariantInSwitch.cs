//@ should_pass
// Accessing Circle's field inside Circle arm — OK
using Houtamelo.Spire;
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
            (Shape.Kind.Circle, _) => s.radius,
            _ => 0,
        };
    }
}
