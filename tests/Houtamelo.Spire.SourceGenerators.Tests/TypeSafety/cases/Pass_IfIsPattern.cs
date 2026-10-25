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
            if (s is { kind: Shape.Kind.Circle, circle: { radius: double r } })
                return true;
            return false;
        }
    }
}
