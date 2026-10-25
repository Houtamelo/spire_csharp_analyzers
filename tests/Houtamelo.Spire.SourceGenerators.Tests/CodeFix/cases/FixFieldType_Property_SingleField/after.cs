using Houtamelo.Spire;
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
            { kind: Shape.Kind.Circle, circle: { radius: var bad  } } => 1,
            { kind: Shape.Kind.Square, square: { sideLength: var x  } } => 2,
        };
    }
}
