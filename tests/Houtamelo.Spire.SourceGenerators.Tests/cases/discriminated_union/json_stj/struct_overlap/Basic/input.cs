using Houtamelo.Spire;

[DiscriminatedUnion(json: JsonLibrary.SystemTextJson)]
partial struct Shape
{
    [Variant] public static partial Shape Circle(double radius);
    [Variant] public static partial Shape Rectangle(float width, float height);
    [Variant] public static partial Shape Point();
}
