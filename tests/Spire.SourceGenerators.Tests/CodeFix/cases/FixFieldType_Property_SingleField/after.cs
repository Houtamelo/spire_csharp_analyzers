using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Shape
    {
        [Variant] public static partial Shape Circle(double radius);
        [Variant] public static partial Shape Square(int sideLength);
    }

    class Consumer
    {
        int Test(Shape s) => s switch
        {
            { kind: Shape.Kind.Circle, radius: var bad } => 1,
            { kind: Shape.Kind.Square, sideLength: var x } => 2,
        };
    }
}
