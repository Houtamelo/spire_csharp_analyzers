//@ should_pass
// Switch statement on Shape? with all variants + default covers null — no diagnostic
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
        void Test(Shape? s)
        {
            switch (s)
            {
                case (Shape.Kind.Circle, double r):
                    break;
                case (Shape.Kind.Square, int x):
                    break;
                default:
                    break;
            }
        }
    }
}
