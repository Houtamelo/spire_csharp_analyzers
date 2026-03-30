using Houtamelo.Spire;

// Custom discriminator name
[DiscriminatedUnion(Layout.Additive, json: JsonLibrary.SystemTextJson, jsonDiscriminator: "type")]
partial struct JsonCustomDiscStj
{
    [Variant] public static partial JsonCustomDiscStj Circle(double radius);
    [Variant] public static partial JsonCustomDiscStj Point();
}

[DiscriminatedUnion(Layout.Additive, json: JsonLibrary.NewtonsoftJson, jsonDiscriminator: "type")]
partial struct JsonCustomDiscNsj
{
    [Variant] public static partial JsonCustomDiscNsj Circle(double radius);
    [Variant] public static partial JsonCustomDiscNsj Point();
}

// JsonName attribute on variants and fields
[DiscriminatedUnion(Layout.Additive, json: JsonLibrary.SystemTextJson)]
partial struct JsonNamedStj
{
    [Variant, JsonName("circle")]
    public static partial JsonNamedStj Circle([JsonName("r")] double radius);
    [Variant, JsonName("rect")]
    public static partial JsonNamedStj Rectangle(float width, float height);
    [Variant, JsonName("pt")]
    public static partial JsonNamedStj Point();
}

[DiscriminatedUnion(Layout.Additive, json: JsonLibrary.NewtonsoftJson)]
partial struct JsonNamedNsj
{
    [Variant, JsonName("circle")]
    public static partial JsonNamedNsj Circle([JsonName("r")] double radius);
    [Variant, JsonName("rect")]
    public static partial JsonNamedNsj Rectangle(float width, float height);
    [Variant, JsonName("pt")]
    public static partial JsonNamedNsj Point();
}
