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
            (Shape.Kind.Rectangle, float width, float height) => throw new System.NotImplementedException(),
            (Shape.Kind.Square, int sideLength) => throw new System.NotImplementedException()
        };
    }
}
