using Houtamelo.Spire;

namespace TestNs
{
    [DiscriminatedUnion(Layout.BoxedFields)]
    partial struct Shape
    {
        [Variant] public static partial Shape Circle(double radius);
        [Variant] public static partial Shape Rectangle(float width, float height);
        [Variant] public static partial Shape Square(int sideLength);
    }
}
