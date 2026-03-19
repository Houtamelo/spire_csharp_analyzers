//@ should_fail
// SPIRE013: accessing Rectangle's field inside Circle arm
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Shape
    {
        [Variant] public static partial Shape Circle(double radius);
        [Variant] public static partial Shape Rectangle(float width, float height);
        [Variant] public static partial Shape Square(int sideLength);
    }
    class C
    {
        double Test(Shape s) => s switch
        {
            (Shape.Kind.Circle, _) => s.rectangle_width, //~ ERROR
            _ => 0,
        };
    }
}
