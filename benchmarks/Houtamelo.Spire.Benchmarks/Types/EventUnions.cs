namespace Houtamelo.Spire.Benchmarks.Types;

// 8 variants with 0-5 fields, mix of managed and unmanaged:
//   Point()                                          — 0 fields
//   Circle(double radius)                            — 1 field, unmanaged
//   Label(string text)                               — 1 field, managed
//   Rectangle(float width, float height)             — 2 fields, unmanaged
//   ColoredLine(int x1, int y1, string color)        — 3 fields, mixed
//   Transform(float x, float y, float z, float w)    — 4 fields, unmanaged
//   RichText(string text, int size, bool bold, string font, double-spacing) — 5 fields, mixed
//   Error(string message)                            — 1 field, managed

[DiscriminatedUnion(Layout.Additive)]
public partial struct EventAdditive
{
    [Variant] public static partial EventAdditive Point();
    [Variant] public static partial EventAdditive Circle(double radius);
    [Variant] public static partial EventAdditive Label(string text);
    [Variant] public static partial EventAdditive Rectangle(float width, float height);
    [Variant] public static partial EventAdditive ColoredLine(int x1, int y1, string color);
    [Variant] public static partial EventAdditive Transform(float x, float y, float z, float w);
    [Variant] public static partial EventAdditive RichText(string text, int size, bool bold, string font, double spacing);
    [Variant] public static partial EventAdditive Error(string message);
}

[DiscriminatedUnion(Layout.BoxedFields)]
public partial struct EventBoxedFields
{
    [Variant] public static partial EventBoxedFields Point();
    [Variant] public static partial EventBoxedFields Circle(double radius);
    [Variant] public static partial EventBoxedFields Label(string text);
    [Variant] public static partial EventBoxedFields Rectangle(float width, float height);
    [Variant] public static partial EventBoxedFields ColoredLine(int x1, int y1, string color);
    [Variant] public static partial EventBoxedFields Transform(float x, float y, float z, float w);
    [Variant] public static partial EventBoxedFields RichText(string text, int size, bool bold, string font, double spacing);
    [Variant] public static partial EventBoxedFields Error(string message);
}

[DiscriminatedUnion(Layout.BoxedTuple)]
public partial struct EventBoxedTuple
{
    [Variant] public static partial EventBoxedTuple Point();
    [Variant] public static partial EventBoxedTuple Circle(double radius);
    [Variant] public static partial EventBoxedTuple Label(string text);
    [Variant] public static partial EventBoxedTuple Rectangle(float width, float height);
    [Variant] public static partial EventBoxedTuple ColoredLine(int x1, int y1, string color);
    [Variant] public static partial EventBoxedTuple Transform(float x, float y, float z, float w);
    [Variant] public static partial EventBoxedTuple RichText(string text, int size, bool bold, string font, double spacing);
    [Variant] public static partial EventBoxedTuple Error(string message);
}

[DiscriminatedUnion(Layout.Overlap)]
public partial struct EventOverlap
{
    [Variant] public static partial EventOverlap Point();
    [Variant] public static partial EventOverlap Circle(double radius);
    [Variant] public static partial EventOverlap Label(string text);
    [Variant] public static partial EventOverlap Rectangle(float width, float height);
    [Variant] public static partial EventOverlap ColoredLine(int x1, int y1, string color);
    [Variant] public static partial EventOverlap Transform(float x, float y, float z, float w);
    [Variant] public static partial EventOverlap RichText(string text, int size, bool bold, string font, double spacing);
    [Variant] public static partial EventOverlap Error(string message);
}

[DiscriminatedUnion(Layout.UnsafeOverlap)]
public partial struct EventUnsafeOverlap
{
    [Variant] public static partial EventUnsafeOverlap Point();
    [Variant] public static partial EventUnsafeOverlap Circle(double radius);
    [Variant] public static partial EventUnsafeOverlap Label(string text);
    [Variant] public static partial EventUnsafeOverlap Rectangle(float width, float height);
    [Variant] public static partial EventUnsafeOverlap ColoredLine(int x1, int y1, string color);
    [Variant] public static partial EventUnsafeOverlap Transform(float x, float y, float z, float w);
    [Variant] public static partial EventUnsafeOverlap RichText(string text, int size, bool bold, string font, double spacing);
    [Variant] public static partial EventUnsafeOverlap Error(string message);
}

[DiscriminatedUnion]
public abstract partial record EventRecord
{
    public sealed partial record Point() : EventRecord;
    public sealed partial record Circle(double Radius) : EventRecord;
    public sealed partial record Label(string Text) : EventRecord;
    public sealed partial record Rectangle(float Width, float Height) : EventRecord;
    public sealed partial record ColoredLine(int X1, int Y1, string Color) : EventRecord;
    public sealed partial record Transform(float X, float Y, float Z, float W) : EventRecord;
    public sealed partial record RichText(string Text, int Size, bool Bold, string Font, double Spacing) : EventRecord;
    public sealed partial record Error(string Message) : EventRecord;
}
