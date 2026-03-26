using Houtamelo.Spire;

[DiscriminatedUnion(Layout.Additive)]
partial struct ShapeAdd
{
    [Variant] public static partial ShapeAdd Circle(double radius);
    [Variant] public static partial ShapeAdd Square(int sideLength);
    [Variant] public static partial ShapeAdd Rectangle(float width, float height);
    [Variant] public static partial ShapeAdd Point();
}

[DiscriminatedUnion(Layout.Additive)]
partial struct VecAdd
{
    [Variant] public static partial VecAdd Vec2(double x, double y);
    [Variant] public static partial VecAdd Vec3(double x, double y, double z);
}

[DiscriminatedUnion(Layout.Additive)]
partial struct OptionAdd<T>
{
    [Variant] public static partial OptionAdd<T> Some(T value);
    [Variant] public static partial OptionAdd<T> None();
}

[DiscriminatedUnion(Layout.Additive)]
partial struct MsgAdd
{
    [Variant] public static partial MsgAdd Label(string text);
    [Variant] public static partial MsgAdd ColoredLine(int x, int y, string color);
    [Variant] public static partial MsgAdd Empty();
}

[DiscriminatedUnion(Layout.Additive, Json = JsonLibrary.SystemTextJson)]
partial struct JsonShapeAddStj
{
    [Variant] public static partial JsonShapeAddStj Circle(double radius);
    [Variant] public static partial JsonShapeAddStj Rectangle(float width, float height);
    [Variant] public static partial JsonShapeAddStj Point();
}

[DiscriminatedUnion(Layout.Additive, Json = JsonLibrary.NewtonsoftJson)]
partial struct JsonShapeAddNsj
{
    [Variant] public static partial JsonShapeAddNsj Circle(double radius);
    [Variant] public static partial JsonShapeAddNsj Rectangle(float width, float height);
    [Variant] public static partial JsonShapeAddNsj Point();
}
