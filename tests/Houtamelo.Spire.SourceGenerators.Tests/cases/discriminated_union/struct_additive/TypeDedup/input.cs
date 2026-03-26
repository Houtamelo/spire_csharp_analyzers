using Spire;

[DiscriminatedUnion(Layout.Additive)]
partial struct Shape
{
    [Variant] public static partial Shape Circle(double radius);
    [Variant] public static partial Shape Rect(double width, double height);
    [Variant] public static partial Shape Square(double side);
    [Variant] public static partial Shape Point();
}
