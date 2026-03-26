using Houtamelo.Spire;

[DiscriminatedUnion(Layout.Additive, Json = JsonLibrary.NewtonsoftJson)]
partial struct Shape
{
    [Variant] public static partial Shape Circle(double radius);
    [Variant] public static partial Shape Square(int sideLength);
    [Variant] public static partial Shape Point();
}
