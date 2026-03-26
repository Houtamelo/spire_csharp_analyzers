//@ should_pass
// Accessing .kind is always OK — it's not a variant field
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
        Shape.Kind Test(Shape s) => s.kind;
    }
}
