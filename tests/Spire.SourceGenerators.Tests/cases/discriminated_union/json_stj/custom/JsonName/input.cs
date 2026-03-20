using Spire;

[DiscriminatedUnion(Json = JsonLibrary.SystemTextJson)]
partial struct Shape
{
    [Variant, JsonName("circle")]
    public static partial Shape Circle([JsonName("r")] double radius);
    [Variant, JsonName("rect")]
    public static partial Shape Rectangle(float width, float height);
    [Variant, JsonName("pt")]
    public static partial Shape Point();
}
