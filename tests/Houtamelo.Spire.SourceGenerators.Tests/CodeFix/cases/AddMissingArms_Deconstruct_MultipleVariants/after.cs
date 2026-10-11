using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Shape
    {
        [Variant] public static partial Shape Circle(double radius);
        [Variant] public static partial Shape Rectangle(float width, float height);
        [Variant] public static partial Shape Square(int sideLength);
    }

    class Consumer
    {
        int Test(Shape s) => s switch
        {
            (Shape.Kind.Circle, double r) => 1,
            { kind: Shape.Kind.Rectangle, width: var width, height: var height } => throw new System.NotImplementedException(),
            { kind: Shape.Kind.Square, sideLength: var sideLength } => throw new System.NotImplementedException()
        };
    }
}
