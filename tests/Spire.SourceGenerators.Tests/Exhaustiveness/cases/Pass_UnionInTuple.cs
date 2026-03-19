//@ should_pass
// Union nested in tuple — exhaustiveness NOT checked
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
        int Test(Shape s, bool flag) => (s, flag) switch
        {
            ((Shape.Kind.Circle, _), true) => 1,
            _ => 0,
        };
    }
}
