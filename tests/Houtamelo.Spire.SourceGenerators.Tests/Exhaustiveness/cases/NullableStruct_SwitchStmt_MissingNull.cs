//@ should_fail
// Switch statement on Shape? covers all variants but not null and no default
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
            switch (s) //~ ERROR
            {
                case (Shape.Kind.Circle, double r):
                    break;
                case (Shape.Kind.Square, int x):
                    break;
            }
        }
    }
}
