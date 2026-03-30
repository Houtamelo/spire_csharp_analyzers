using Houtamelo.Spire;

[DiscriminatedUnion(Layout.UnsafeOverlap)]
partial struct ShapeUo
{
    [Variant] public static partial ShapeUo Circle(double radius);
    [Variant] public static partial ShapeUo Square(int sideLength);
    [Variant] public static partial ShapeUo Rectangle(float width, float height);
    [Variant] public static partial ShapeUo Point();
}

[DiscriminatedUnion(Layout.UnsafeOverlap)]
partial struct VecUo
{
    [Variant] public static partial VecUo Vec2(double x, double y);
    [Variant] public static partial VecUo Vec3(double x, double y, double z);
}

[DiscriminatedUnion(Layout.UnsafeOverlap)]
partial struct MsgUo
{
    [Variant] public static partial MsgUo Label(string text);
    [Variant] public static partial MsgUo ColoredLine(int x, int y, string color);
    [Variant] public static partial MsgUo Empty();
}

[DiscriminatedUnion(Layout.UnsafeOverlap, json: JsonLibrary.SystemTextJson)]
partial struct JsonShapeUoStj
{
    [Variant] public static partial JsonShapeUoStj Circle(double radius);
    [Variant] public static partial JsonShapeUoStj Rectangle(float width, float height);
    [Variant] public static partial JsonShapeUoStj Point();
}

[DiscriminatedUnion(Layout.UnsafeOverlap, json: JsonLibrary.NewtonsoftJson)]
partial struct JsonShapeUoNsj
{
    [Variant] public static partial JsonShapeUoNsj Circle(double radius);
    [Variant] public static partial JsonShapeUoNsj Rectangle(float width, float height);
    [Variant] public static partial JsonShapeUoNsj Point();
}
