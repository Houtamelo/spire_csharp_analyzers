//@ should_pass
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
        int Test(Shape s) => s switch
        {
            { kind: Shape.Kind.Circle, circle: { radius: double _ } } => 1,
            { kind: Shape.Kind.Square, square: { sideLength: int _ } } => 2,
        };
    }
}
