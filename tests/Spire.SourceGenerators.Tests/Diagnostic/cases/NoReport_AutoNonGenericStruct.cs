//@ should_pass
// No diagnostics for Auto layout on non-generic struct
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Shape
    {
        [Variant] static partial void Circle(double radius);
        [Variant] static partial void Square(int sideLength);
    }
}
