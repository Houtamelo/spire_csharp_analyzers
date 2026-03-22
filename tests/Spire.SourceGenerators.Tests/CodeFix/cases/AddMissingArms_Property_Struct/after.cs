using Spire;
namespace TestNs
{
    [DiscriminatedUnion(PublicProperties = true)]
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
            { kind: Shape.Kind.Circle, radius: var r } => 1,
            { kind: Shape.Kind.Rectangle, width: var w, height: var h } => 2,
            { kind: Shape.Kind.Square, sideLength: int sideLenth } => throw new System.NotImplementedException(),
        };
    }
}
