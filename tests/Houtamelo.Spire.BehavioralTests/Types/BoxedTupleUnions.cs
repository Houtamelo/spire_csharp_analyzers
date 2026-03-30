using Houtamelo.Spire;

[DiscriminatedUnion(Layout.BoxedTuple)]
partial struct ShapeBt
{
    [Variant] public static partial ShapeBt Circle(double radius);
    [Variant] public static partial ShapeBt Square(int sideLength);
    [Variant] public static partial ShapeBt Rectangle(float width, float height);
    [Variant] public static partial ShapeBt Point();
}

[DiscriminatedUnion(Layout.BoxedTuple)]
partial struct VecBt
{
    [Variant] public static partial VecBt Vec2(double x, double y);
    [Variant] public static partial VecBt Vec3(double x, double y, double z);
}

[DiscriminatedUnion(Layout.BoxedTuple)]
partial struct OptionBt<T>
{
    [Variant] public static partial OptionBt<T> Some(T value);
    [Variant] public static partial OptionBt<T> None();
}

[DiscriminatedUnion(Layout.BoxedTuple)]
partial struct MsgBt
{
    [Variant] public static partial MsgBt Label(string text);
    [Variant] public static partial MsgBt ColoredLine(int x, int y, string color);
    [Variant] public static partial MsgBt Empty();
}

[DiscriminatedUnion(Layout.BoxedTuple, json: JsonLibrary.SystemTextJson)]
partial struct JsonShapeBtStj
{
    [Variant] public static partial JsonShapeBtStj Circle(double radius);
    [Variant] public static partial JsonShapeBtStj Rectangle(float width, float height);
    [Variant] public static partial JsonShapeBtStj Point();
}

[DiscriminatedUnion(Layout.BoxedTuple, json: JsonLibrary.NewtonsoftJson)]
partial struct JsonShapeBtNsj
{
    [Variant] public static partial JsonShapeBtNsj Circle(double radius);
    [Variant] public static partial JsonShapeBtNsj Rectangle(float width, float height);
    [Variant] public static partial JsonShapeBtNsj Point();
}
