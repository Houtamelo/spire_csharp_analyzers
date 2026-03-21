using Spire;

namespace Spire.Benchmarks;

// JSON-enabled variants for serialization benchmarks

[DiscriminatedUnion(Layout.Additive, Json = JsonLibrary.SystemTextJson | JsonLibrary.NewtonsoftJson)]
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

[DiscriminatedUnion(Json = JsonLibrary.SystemTextJson | JsonLibrary.NewtonsoftJson)]
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
