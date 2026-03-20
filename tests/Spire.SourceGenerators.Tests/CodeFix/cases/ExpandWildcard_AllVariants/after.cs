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

    class Consumer
    {
        int Test(Shape s) => s switch
        {
            (Shape.Kind.Circle, double radius) => 0,
            (Shape.Kind.Rectangle, float width, float height) => 0,
            (Shape.Kind.Square, int sideLength) => 0
        };
    }
}
