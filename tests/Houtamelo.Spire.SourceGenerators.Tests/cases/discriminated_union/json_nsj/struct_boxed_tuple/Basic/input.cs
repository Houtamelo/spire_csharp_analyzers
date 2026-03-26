using Houtamelo.Spire;

[DiscriminatedUnion(Layout.BoxedTuple, Json = JsonLibrary.NewtonsoftJson)]
partial struct Shape
{
    [Variant] public static partial Shape Circle(double radius);
    [Variant] public static partial Shape Rectangle(float width, float height);
    [Variant] public static partial Shape Point();
}
