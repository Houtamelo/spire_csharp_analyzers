//@ should_pass
// Switch statement with default clause covering missing variants — no diagnostic
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
        void Test(Shape s)
        {
            switch (s)
            {
                case (Shape.Kind.Circle, double r):
                    break;
                default:
                    break;
            }
        }
    }
}
