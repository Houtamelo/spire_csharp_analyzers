//@ should_pass
// Property pattern — inherently guarded
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
            { tag: Shape.Kind.Circle, circle_radius: var r } => r,
            _ => 0,
        };
    }
}
