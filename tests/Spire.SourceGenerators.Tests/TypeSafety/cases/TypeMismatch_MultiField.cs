//@ should_fail
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
    class C
    {
        int Test(Shape s) => s switch
        {
            (Shape.Kind.Rectangle, float w, int h) => 1, //~ ERROR
            _ => 0,
        };
    }
}
