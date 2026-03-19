using Spire;

namespace TestNs
{
    [DiscriminatedUnion(Layout.BoxedTuple)]
    partial struct Shape
    {
        [Variant] static partial void Circle(double radius);
        [Variant] static partial void Rectangle(float width, float height);
        [Variant] static partial void Square(int sideLength);
    }
}
