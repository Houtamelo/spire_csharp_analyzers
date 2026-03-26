//@ should_pass
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Shape
    {
        [Variant] public static partial Shape Circle(double radius);
        [Variant] public static partial Shape Square(int sideLength);
    }
    class C
    {
        bool Test(Shape s)
        {
            if (s is (Shape.Kind.Circle, double r))
                return true;
            return false;
        }
    }
}
