//@ should_fail
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
            { kind: Shape.Kind.Circle, circle: { radius: string _ } } => 1, //~ ERROR
            { kind: Shape.Kind.Square, square: { sideLength: int x } } => 2,
        };
    }
}
