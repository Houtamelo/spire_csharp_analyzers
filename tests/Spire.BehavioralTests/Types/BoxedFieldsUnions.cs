using Spire;

[DiscriminatedUnion(Layout.BoxedFields)]
partial struct ShapeBf
{
    [Variant] public static partial ShapeBf Circle(double radius);
    [Variant] public static partial ShapeBf Square(int sideLength);
    [Variant] public static partial ShapeBf Rectangle(float width, float height);
    [Variant] public static partial ShapeBf Point();
}

[DiscriminatedUnion(Layout.BoxedFields)]
partial struct VecBf
{
    [Variant] public static partial VecBf Vec2(double x, double y);
    [Variant] public static partial VecBf Vec3(double x, double y, double z);
}

[DiscriminatedUnion(Layout.BoxedFields)]
partial struct OptionBf<T>
{
    [Variant] public static partial OptionBf<T> Some(T value);
    [Variant] public static partial OptionBf<T> None();
}

[DiscriminatedUnion(Layout.BoxedFields)]
partial struct MsgBf
{
    [Variant] public static partial MsgBf Label(string text);
    [Variant] public static partial MsgBf ColoredLine(int x, int y, string color);
    [Variant] public static partial MsgBf Empty();
}

[DiscriminatedUnion(Layout.BoxedFields, Json = JsonLibrary.SystemTextJson)]
partial struct JsonShapeBfStj
{
    [Variant] public static partial JsonShapeBfStj Circle(double radius);
    [Variant] public static partial JsonShapeBfStj Rectangle(float width, float height);
    [Variant] public static partial JsonShapeBfStj Point();
}

[DiscriminatedUnion(Layout.BoxedFields, Json = JsonLibrary.NewtonsoftJson)]
partial struct JsonShapeBfNsj
{
    [Variant] public static partial JsonShapeBfNsj Circle(double radius);
    [Variant] public static partial JsonShapeBfNsj Rectangle(float width, float height);
    [Variant] public static partial JsonShapeBfNsj Point();
}
