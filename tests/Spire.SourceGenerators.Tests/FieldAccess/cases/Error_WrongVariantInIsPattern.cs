//@ should_fail
// SPIRE013: accessing Square's field inside Circle is-pattern guard
using Spire;
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
        int Test(Shape s)
        {
            if (s is (Shape.Kind.Circle, _))
            {
                return s.square_sideLength; //~ ERROR
            }
            return 0;
        }
    }
}
