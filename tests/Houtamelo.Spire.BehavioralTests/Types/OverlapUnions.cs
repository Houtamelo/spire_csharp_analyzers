using Spire;

[DiscriminatedUnion(Layout.Overlap)]
partial struct ShapeOvl
{
    [Variant] public static partial ShapeOvl Circle(double radius);
    [Variant] public static partial ShapeOvl Square(int sideLength);
    [Variant] public static partial ShapeOvl Rectangle(float width, float height);
    [Variant] public static partial ShapeOvl Point();
}

[DiscriminatedUnion(Layout.Overlap)]
partial struct VecOvl
{
    [Variant] public static partial VecOvl Vec2(double x, double y);
    [Variant] public static partial VecOvl Vec3(double x, double y, double z);
}

[DiscriminatedUnion(Layout.Overlap)]
partial struct MsgOvl
{
    [Variant] public static partial MsgOvl Label(string text);
    [Variant] public static partial MsgOvl ColoredLine(int x, int y, string color);
    [Variant] public static partial MsgOvl Empty();
}

[DiscriminatedUnion(Layout.Overlap, Json = JsonLibrary.SystemTextJson)]
partial struct JsonShapeOvlStj
{
    [Variant] public static partial JsonShapeOvlStj Circle(double radius);
    [Variant] public static partial JsonShapeOvlStj Rectangle(float width, float height);
    [Variant] public static partial JsonShapeOvlStj Point();
}

[DiscriminatedUnion(Layout.Overlap, Json = JsonLibrary.NewtonsoftJson)]
partial struct JsonShapeOvlNsj
{
    [Variant] public static partial JsonShapeOvlNsj Circle(double radius);
    [Variant] public static partial JsonShapeOvlNsj Rectangle(float width, float height);
    [Variant] public static partial JsonShapeOvlNsj Point();
}
