//@ should_pass
// All pattern types match Deconstruct params
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
            { kind: Shape.Kind.Circle, circle: { radius: double r } } => 1,
            { kind: Shape.Kind.Square, square: { sideLength: int x } } => 2,
        };
    }
}
