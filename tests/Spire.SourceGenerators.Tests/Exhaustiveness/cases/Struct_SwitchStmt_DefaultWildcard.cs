//@ should_fail
// Switch statement with default clause covering missing variant — SPIRE010
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
        void Test(Shape s)
        {
            switch (s) //~ ERROR
            {
                case (Shape.Kind.Circle, double r):
                    break;
                default:
                    break;
            }
        }
    }
}
