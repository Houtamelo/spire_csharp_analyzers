//@ should_pass
// Switch statement fully covered — no diagnostic
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
            switch (s)
            {
                case (Shape.Kind.Circle, double r):
                    break;
                case (Shape.Kind.Rectangle, var w, var h):
                    break;
                case (Shape.Kind.Square, int x):
                    break;
            }
        }
    }
}
