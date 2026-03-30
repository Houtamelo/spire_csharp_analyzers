namespace Houtamelo.Spire.Benchmarks.Types;

// JSON-enabled variants for serialization benchmarks

[DiscriminatedUnion(Layout.Additive, json: JsonLibrary.SystemTextJson | JsonLibrary.NewtonsoftJson)]
public partial struct EventAdditiveJson
{
    [Variant] public static partial EventAdditiveJson Point();
    [Variant] public static partial EventAdditiveJson Circle(double radius);
    [Variant] public static partial EventAdditiveJson Label(string text);
    [Variant] public static partial EventAdditiveJson Rectangle(float width, float height);
    [Variant] public static partial EventAdditiveJson ColoredLine(int x1, int y1, string color);
    [Variant] public static partial EventAdditiveJson Transform(float x, float y, float z, float w);
    [Variant] public static partial EventAdditiveJson RichText(string text, int size, bool bold, string font, double spacing);
    [Variant] public static partial EventAdditiveJson Error(string message);
}

[DiscriminatedUnion(Layout.BoxedFields, json: JsonLibrary.SystemTextJson | JsonLibrary.NewtonsoftJson)]
public partial struct EventBoxedFieldsJson
{
    [Variant] public static partial EventBoxedFieldsJson Point();
    [Variant] public static partial EventBoxedFieldsJson Circle(double radius);
    [Variant] public static partial EventBoxedFieldsJson Label(string text);
    [Variant] public static partial EventBoxedFieldsJson Rectangle(float width, float height);
    [Variant] public static partial EventBoxedFieldsJson ColoredLine(int x1, int y1, string color);
    [Variant] public static partial EventBoxedFieldsJson Transform(float x, float y, float z, float w);
    [Variant] public static partial EventBoxedFieldsJson RichText(string text, int size, bool bold, string font, double spacing);
    [Variant] public static partial EventBoxedFieldsJson Error(string message);
}

[DiscriminatedUnion(Layout.BoxedTuple, json: JsonLibrary.SystemTextJson | JsonLibrary.NewtonsoftJson)]
public partial struct EventBoxedTupleJson
{
    [Variant] public static partial EventBoxedTupleJson Point();
    [Variant] public static partial EventBoxedTupleJson Circle(double radius);
    [Variant] public static partial EventBoxedTupleJson Label(string text);
    [Variant] public static partial EventBoxedTupleJson Rectangle(float width, float height);
    [Variant] public static partial EventBoxedTupleJson ColoredLine(int x1, int y1, string color);
    [Variant] public static partial EventBoxedTupleJson Transform(float x, float y, float z, float w);
    [Variant] public static partial EventBoxedTupleJson RichText(string text, int size, bool bold, string font, double spacing);
    [Variant] public static partial EventBoxedTupleJson Error(string message);
}

[DiscriminatedUnion(Layout.Overlap, json: JsonLibrary.SystemTextJson | JsonLibrary.NewtonsoftJson)]
public partial struct EventOverlapJson
{
    [Variant] public static partial EventOverlapJson Point();
    [Variant] public static partial EventOverlapJson Circle(double radius);
    [Variant] public static partial EventOverlapJson Label(string text);
    [Variant] public static partial EventOverlapJson Rectangle(float width, float height);
    [Variant] public static partial EventOverlapJson ColoredLine(int x1, int y1, string color);
    [Variant] public static partial EventOverlapJson Transform(float x, float y, float z, float w);
    [Variant] public static partial EventOverlapJson RichText(string text, int size, bool bold, string font, double spacing);
    [Variant] public static partial EventOverlapJson Error(string message);
}

[DiscriminatedUnion(Layout.UnsafeOverlap, json: JsonLibrary.SystemTextJson | JsonLibrary.NewtonsoftJson)]
public partial struct EventUnsafeOverlapJson
{
    [Variant] public static partial EventUnsafeOverlapJson Point();
    [Variant] public static partial EventUnsafeOverlapJson Circle(double radius);
    [Variant] public static partial EventUnsafeOverlapJson Label(string text);
    [Variant] public static partial EventUnsafeOverlapJson Rectangle(float width, float height);
    [Variant] public static partial EventUnsafeOverlapJson ColoredLine(int x1, int y1, string color);
    [Variant] public static partial EventUnsafeOverlapJson Transform(float x, float y, float z, float w);
    [Variant] public static partial EventUnsafeOverlapJson RichText(string text, int size, bool bold, string font, double spacing);
    [Variant] public static partial EventUnsafeOverlapJson Error(string message);
}

[DiscriminatedUnion(json: JsonLibrary.SystemTextJson | JsonLibrary.NewtonsoftJson)]
public abstract partial record EventRecordJson
{
    public sealed partial record Point() : EventRecordJson;
    public sealed partial record Circle(double Radius) : EventRecordJson;
    public sealed partial record Label(string Text) : EventRecordJson;
    public sealed partial record Rectangle(float Width, float Height) : EventRecordJson;
    public sealed partial record ColoredLine(int X1, int Y1, string Color) : EventRecordJson;
    public sealed partial record Transform(float X, float Y, float Z, float W) : EventRecordJson;
    public sealed partial record RichText(string Text, int Size, bool Bold, string Font, double Spacing) : EventRecordJson;
    public sealed partial record Error(string Message) : EventRecordJson;
}