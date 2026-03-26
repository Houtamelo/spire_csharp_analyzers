using Houtamelo.Spire;

[DiscriminatedUnion(Layout.UnsafeOverlap)]
partial struct Shape
{
    [Variant] public static partial Shape Circle(double radius);
    [Variant] public static partial Shape Rect(float width, float height);
    [Variant] public static partial Shape Point();
}
