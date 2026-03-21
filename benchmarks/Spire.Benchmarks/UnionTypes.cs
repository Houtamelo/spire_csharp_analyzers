using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spire.Benchmarks;

// 8 variants with 1-5 fields, mix of managed and unmanaged:
//   Point()                                          — 0 fields
//   Circle(double radius)                            — 1 field, unmanaged
//   Label(string text)                               — 1 field, managed
//   Rectangle(float width, float height)             — 2 fields, unmanaged
//   ColoredLine(int x1, int y1, string color)        — 3 fields, mixed
//   Transform(float x, float y, float z, float w)    — 4 fields, unmanaged
//   RichText(string text, int size, bool bold, string font, double spacing) — 5 fields, mixed

public enum EventKind : byte {
    Point,
    Circle,
    Label,
    Rectangle,
    ColoredLine,
    Transform,
    RichText,
    Error,
}

// ── Layout A: Multi object? fields (max 5 fields) ──

public readonly struct EventMultiObj {
    public readonly EventKind tag;
    readonly object? _f0, _f1, _f2, _f3, _f4;

    EventMultiObj(EventKind tag, object? f0, object? f1, object? f2, object? f3, object? f4) {
        this.tag = tag; _f0 = f0; _f1 = f1; _f2 = f2; _f3 = f3; _f4 = f4;
    }

    public static EventMultiObj NewPoint() => new(EventKind.Point, null, null, null, null, null);
    public static EventMultiObj NewCircle(double radius) => new(EventKind.Circle, radius, null, null, null, null);
    public static EventMultiObj NewLabel(string text) => new(EventKind.Label, text, null, null, null, null);
    public static EventMultiObj NewRectangle(float w, float h) => new(EventKind.Rectangle, w, h, null, null, null);
    public static EventMultiObj NewColoredLine(int x1, int y1, string color) => new(EventKind.ColoredLine, x1, y1, color, null, null);
    public static EventMultiObj NewTransform(float x, float y, float z, float w) => new(EventKind.Transform, x, y, z, w, null);
    public static EventMultiObj NewRichText(string text, int size, bool bold, string font, double spacing) => new(EventKind.RichText, text, size, bold, font, spacing);
    public static EventMultiObj NewError(string message) => new(EventKind.Error, message, null, null, null, null);

    // 1-param shared (Point, Circle, Label, Error)
    public void Deconstruct(out EventKind kind, out object? f0) {
        kind = tag; f0 = _f0;
    }
    // 2-param shared (Rectangle)
    public void Deconstruct(out EventKind kind, out object? f0, out object? f1) {
        kind = tag; f0 = _f0; f1 = _f1;
    }
    // 3-param shared (ColoredLine)
    public void Deconstruct(out EventKind kind, out object? f0, out object? f1, out object? f2) {
        kind = tag; f0 = _f0; f1 = _f1; f2 = _f2;
    }
    // 4-param unique (Transform) — typed!
    public void Deconstruct(out EventKind kind, out float x, out float y, out float z, out float w) {
        kind = tag;
        x = (float)_f0!; y = (float)_f1!; z = (float)_f2!; w = (float)_f3!;
    }
    // 5-param unique (RichText) — typed!
    public void Deconstruct(out EventKind kind, out string text, out int size, out bool bold, out string font, out double spacing) {
        kind = tag;
        text = (string)_f0!; size = (int)_f1!; bold = (bool)_f2!; font = (string)_f3!; spacing = (double)_f4!;
    }
}

// ── Layout B: Single object? payload (tuple approach) ──

public readonly struct EventTupleObj {
    public readonly EventKind tag;
    readonly object? _payload;

    EventTupleObj(EventKind tag, object? payload) {
        this.tag = tag; _payload = payload;
    }

    public static EventTupleObj NewPoint() => new(EventKind.Point, null);
    public static EventTupleObj NewCircle(double radius) => new(EventKind.Circle, radius);
    public static EventTupleObj NewLabel(string text) => new(EventKind.Label, text);
    public static EventTupleObj NewRectangle(float w, float h) => new(EventKind.Rectangle, (w, h));
    public static EventTupleObj NewColoredLine(int x1, int y1, string color) => new(EventKind.ColoredLine, (x1, y1, color));
    public static EventTupleObj NewTransform(float x, float y, float z, float w) => new(EventKind.Transform, (x, y, z, w));
    public static EventTupleObj NewRichText(string text, int size, bool bold, string font, double spacing) => new(EventKind.RichText, (text, size, bold, font, spacing));
    public static EventTupleObj NewError(string message) => new(EventKind.Error, message);

    public void Deconstruct(out EventKind kind, out object? payload) {
        kind = tag; payload = _payload;
    }
    // 4-param unique (Transform) — typed via tuple cast
    public void Deconstruct(out EventKind kind, out float x, out float y, out float z, out float w) {
        kind = tag;
        var t = ((float, float, float, float))_payload!;
        x = t.Item1; y = t.Item2; z = t.Item3; w = t.Item4;
    }
    // 5-param unique (RichText) — typed via tuple cast
    public void Deconstruct(out EventKind kind, out string text, out int size, out bool bold, out string font, out double spacing) {
        kind = tag;
        var t = ((string, int, bool, string, double))_payload!;
        text = t.Item1; size = t.Item2; bold = t.Item3; font = t.Item4; spacing = t.Item5;
    }
}

// ── Layout C: Abstract class hierarchy ──

public abstract class EventClass {
    public sealed class PointClass : EventClass;
    public sealed class CircleClass(double Radius) : EventClass { public double Radius { get; } = Radius; }
    public sealed class LabelClass(string Text) : EventClass { public string Text { get; } = Text; }
    public sealed class RectangleClass(float Width, float Height) : EventClass {
        public float Width { get; } = Width;
        public float Height { get; } = Height;
    }
    public sealed class ColoredLineClass(int X1, int Y1, string Color) : EventClass {
        public int X1 { get; } = X1;
        public int Y1 { get; } = Y1;
        public string Color { get; } = Color;
    }
    public sealed class TransformClass(float X, float Y, float Z, float W) : EventClass {
        public float X { get; } = X;
        public float Y { get; } = Y;
        public float Z { get; } = Z;
        public float W { get; } = W;
    }
    public sealed class RichTextClass(string Text, int Size, bool Bold, string Font, double Spacing) : EventClass {
        public string Text { get; } = Text;
        public int Size { get; } = Size;
        public bool Bold { get; } = Bold;
        public string Font { get; } = Font;
        public double Spacing { get; } = Spacing;
    }
    public sealed class ErrorClass(string Message) : EventClass { public string Message { get; } = Message; }

    public static EventClass NewPoint() => new PointClass();
    public static EventClass NewCircle(double radius) => new CircleClass(radius);
    public static EventClass NewLabel(string text) => new LabelClass(text);
    public static EventClass NewRectangle(float w, float h) => new RectangleClass(w, h);
    public static EventClass NewColoredLine(int x1, int y1, string color) => new ColoredLineClass(x1, y1, color);
    public static EventClass NewTransform(float x, float y, float z, float w) => new TransformClass(x, y, z, w);
    public static EventClass NewRichText(string text, int size, bool bold, string font, double spacing) => new RichTextClass(text, size, bold, font, spacing);
    public static EventClass NewError(string message) => new ErrorClass(message);
}

// ── Layout D: Abstract record hierarchy ──

public abstract record EventRecord {
    public sealed record PointRecord : EventRecord;
    public sealed record CircleRecord(double Radius) : EventRecord;
    public sealed record LabelRecord(string Text) : EventRecord;
    public sealed record RectangleRecord(float Width, float Height) : EventRecord;
    public sealed record ColoredLineRecord(int X1, int Y1, string Color) : EventRecord;
    public sealed record TransformRecord(float X, float Y, float Z, float W) : EventRecord;
    public sealed record RichTextRecord(string Text, int Size, bool Bold, string Font, double Spacing) : EventRecord;
    public sealed record ErrorRecord(string Message) : EventRecord;

    public static EventRecord NewPoint() => new PointRecord();
    public static EventRecord NewCircle(double radius) => new CircleRecord(radius);
    public static EventRecord NewLabel(string text) => new LabelRecord(text);
    public static EventRecord NewRectangle(float w, float h) => new RectangleRecord(w, h);
    public static EventRecord NewColoredLine(int x1, int y1, string color) => new ColoredLineRecord(x1, y1, color);
    public static EventRecord NewTransform(float x, float y, float z, float w) => new TransformRecord(x, y, z, w);
    public static EventRecord NewRichText(string text, int size, bool bold, string font, double spacing) => new RichTextRecord(text, size, bold, font, spacing);
    public static EventRecord NewError(string message) => new ErrorRecord(message);
}

// ── Layout E: Hybrid explicit (unmanaged fields overlap, managed fields separate) ──
//
// Memory layout (40 bytes):
//   Offset 0:  EventKind tag (4 bytes with padding for alignment)
//   Offset 4-19: UNMANAGED overlap region (16 bytes, fits all unmanaged fields)
//     Circle:      double radius at [4]
//     Rectangle:   float width at [4], float height at [8]
//     ColoredLine: int x1 at [4], int y1 at [8]
//     Transform:   float x at [4], y at [8], z at [12], w at [16]
//     RichText:    double spacing at [4], int size at [12], bool bold at [16]
//   Offset 24-31: MANAGED ref_0 (shared: Label.text, ColoredLine.color, RichText.text, Error.message)
//   Offset 32-39: MANAGED ref_1 (RichText.font only)

[StructLayout(LayoutKind.Explicit)]
public readonly struct EventHybrid {
    // Tag
    [FieldOffset(0)] public readonly EventKind tag;

    // ── Unmanaged overlap region (offset 4-19) ──

    // Circle
    [FieldOffset(4)] public readonly double circle_radius;          // [4-11]

    // Rectangle
    [FieldOffset(4)] public readonly float rect_width;              // [4-7]
    [FieldOffset(8)] public readonly float rect_height;             // [8-11]

    // ColoredLine (unmanaged part)
    [FieldOffset(4)] public readonly int coloredline_x1;            // [4-7]
    [FieldOffset(8)] public readonly int coloredline_y1;            // [8-11]

    // Transform
    [FieldOffset(4)] public readonly float transform_x;             // [4-7]
    [FieldOffset(8)] public readonly float transform_y;             // [8-11]
    [FieldOffset(12)] public readonly float transform_z;            // [12-15]
    [FieldOffset(16)] public readonly float transform_w;            // [16-19]

    // RichText (unmanaged part, reordered: double first for overlap with circle_radius)
    [FieldOffset(4)] public readonly double richtext_spacing;       // [4-11]
    [FieldOffset(12)] public readonly int richtext_size;            // [12-15]
    [FieldOffset(16)] public readonly bool richtext_bold;           // [16]

    // ── Managed region (offset 24+, does NOT overlap with unmanaged) ──

    // Shared string slot: Label.text, ColoredLine.color, RichText.text, Error.message
    [FieldOffset(24)] public readonly string? ref_0;                // [24-31]

    // Second string slot: RichText.font only
    [FieldOffset(32)] public readonly string? ref_1;                // [32-39]

    // ── Constructors ──

    EventHybrid(EventKind tag) : this() { this.tag = tag; }

    public static EventHybrid NewPoint() => new(EventKind.Point);

    public static EventHybrid NewCircle(double radius) {
        var s = new EventHybrid(EventKind.Circle);
        Unsafe.AsRef(in s.circle_radius) = radius;
        return s;
    }

    public static EventHybrid NewLabel(string text) {
        var s = new EventHybrid(EventKind.Label);
        Unsafe.AsRef(in s.ref_0) = text;
        return s;
    }

    public static EventHybrid NewRectangle(float w, float h) {
        var s = new EventHybrid(EventKind.Rectangle);
        Unsafe.AsRef(in s.rect_width) = w;
        Unsafe.AsRef(in s.rect_height) = h;
        return s;
    }

    public static EventHybrid NewColoredLine(int x1, int y1, string color) {
        var s = new EventHybrid(EventKind.ColoredLine);
        Unsafe.AsRef(in s.coloredline_x1) = x1;
        Unsafe.AsRef(in s.coloredline_y1) = y1;
        Unsafe.AsRef(in s.ref_0) = color;
        return s;
    }

    public static EventHybrid NewTransform(float x, float y, float z, float w) {
        var s = new EventHybrid(EventKind.Transform);
        Unsafe.AsRef(in s.transform_x) = x;
        Unsafe.AsRef(in s.transform_y) = y;
        Unsafe.AsRef(in s.transform_z) = z;
        Unsafe.AsRef(in s.transform_w) = w;
        return s;
    }

    public static EventHybrid NewRichText(string text, int size, bool bold, string font, double spacing) {
        var s = new EventHybrid(EventKind.RichText);
        Unsafe.AsRef(in s.richtext_spacing) = spacing;
        Unsafe.AsRef(in s.richtext_size) = size;
        Unsafe.AsRef(in s.richtext_bold) = bold;
        Unsafe.AsRef(in s.ref_0) = text;
        Unsafe.AsRef(in s.ref_1) = font;
        return s;
    }

    public static EventHybrid NewError(string message) {
        var s = new EventHybrid(EventKind.Error);
        Unsafe.AsRef(in s.ref_0) = message;
        return s;
    }
}

// ── Layout F: Additive (typed field dedup, no boxing for value types) ──
//
// double slots: max(Circle=1, RichText.spacing=1) = 1
// float  slots: max(Rect=2, Transform=4) = 4
// int    slots: max(ColoredLine=2, RichText.size=1) = 2
// bool   slots: max(RichText=1) = 1
// object? slots (ref dedup): max(ColoredLine=1, RichText=2, Label=1, Error=1) = 2

public struct EventAdditive {
    public readonly EventKind tag;
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    internal double _s0;
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    internal float _s1, _s2, _s3, _s4;
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    internal int _s5, _s6;
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    internal bool _s7;
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    internal object? _s8, _s9;

    EventAdditive(EventKind tag, double s0, float s1, float s2, float s3, float s4,
                  int s5, int s6, bool s7, object? s8, object? s9) {
        this.tag = tag;
        _s0 = s0; _s1 = s1; _s2 = s2; _s3 = s3; _s4 = s4;
        _s5 = s5; _s6 = s6; _s7 = s7; _s8 = s8; _s9 = s9;
    }

    public static EventAdditive NewPoint()
        => new(EventKind.Point, default, default, default, default, default, default, default, default, null, null);
    public static EventAdditive NewCircle(double radius)
        => new(EventKind.Circle, radius, default, default, default, default, default, default, default, null, null);
    public static EventAdditive NewLabel(string text)
        => new(EventKind.Label, default, default, default, default, default, default, default, default, text, null);
    public static EventAdditive NewRectangle(float w, float h)
        => new(EventKind.Rectangle, default, w, h, default, default, default, default, default, null, null);
    public static EventAdditive NewColoredLine(int x1, int y1, string color)
        => new(EventKind.ColoredLine, default, default, default, default, default, x1, y1, default, color, null);
    public static EventAdditive NewTransform(float x, float y, float z, float w)
        => new(EventKind.Transform, default, x, y, z, w, default, default, default, null, null);
    public static EventAdditive NewRichText(string text, int size, bool bold, string font, double spacing)
        => new(EventKind.RichText, spacing, default, default, default, default, size, default, bold, text, font);
    public static EventAdditive NewError(string message)
        => new(EventKind.Error, default, default, default, default, default, default, default, default, message, null);

    // Typed accessors
    public double CircleRadius => _s0;
    public (float w, float h) Rect => (_s1, _s2);
    public (int x1, int y1) ColoredLineCoords => (_s5, _s6);
    public string? ColoredLineColor => (string?)_s8;
    public (float x, float y, float z, float w) Transform => (_s1, _s2, _s3, _s4);
    public double RichTextSpacing => _s0;
    public int RichTextSize => _s5;
    public bool RichTextBold => _s7;
    public string? RichTextText => (string?)_s8;
    public string? RichTextFont => (string?)_s9;
    public string? LabelText => (string?)_s8;
    public string? ErrorMessage => (string?)_s8;
}

// ── Layout G: UnsafeOverlap (InlineArray) — byte buffer for unmanaged, object? dedup for managed ──
//
// Unmanaged bytes per variant:
//   Point=0, Circle=8, Label=0, Rect=8, ColoredLine=8, Transform=16, RichText=17, Error=0
//   Max = 17 bytes → buffer [InlineArray(17)]
// Ref slots: max(RichText=2) = 2

public struct EventUnsafeInline {
    public readonly EventKind tag;

    [InlineArray(17)]
    internal struct _Buffer { internal byte _element; }

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    internal _Buffer _data;
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    internal object? _s0, _s1;

    EventUnsafeInline(EventKind tag) { this.tag = tag; _data = default; _s0 = null; _s1 = null; }

    public static EventUnsafeInline NewPoint() => new(EventKind.Point);
    public static EventUnsafeInline NewCircle(double radius) {
        var s = new EventUnsafeInline(EventKind.Circle);
        Unsafe.WriteUnaligned(ref s._data[0], radius);
        return s;
    }
    public static EventUnsafeInline NewLabel(string text) {
        var s = new EventUnsafeInline(EventKind.Label);
        s._s0 = text;
        return s;
    }
    public static EventUnsafeInline NewRectangle(float w, float h) {
        var s = new EventUnsafeInline(EventKind.Rectangle);
        Unsafe.WriteUnaligned(ref s._data[0], w);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref s._data[0], 4), h);
        return s;
    }
    public static EventUnsafeInline NewColoredLine(int x1, int y1, string color) {
        var s = new EventUnsafeInline(EventKind.ColoredLine);
        Unsafe.WriteUnaligned(ref s._data[0], x1);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref s._data[0], 4), y1);
        s._s0 = color;
        return s;
    }
    public static EventUnsafeInline NewTransform(float x, float y, float z, float w) {
        var s = new EventUnsafeInline(EventKind.Transform);
        Unsafe.WriteUnaligned(ref s._data[0], x);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref s._data[0], 4), y);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref s._data[0], 8), z);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref s._data[0], 12), w);
        return s;
    }
    public static EventUnsafeInline NewRichText(string text, int size, bool bold, string font, double spacing) {
        var s = new EventUnsafeInline(EventKind.RichText);
        Unsafe.WriteUnaligned(ref s._data[0], spacing);     // 8 bytes at offset 0
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref s._data[0], 8), size);   // 4 bytes at offset 8
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref s._data[0], 12), bold);  // 1 byte at offset 12
        s._s0 = text;
        s._s1 = font;
        return s;
    }
    public static EventUnsafeInline NewError(string message) {
        var s = new EventUnsafeInline(EventKind.Error);
        s._s0 = message;
        return s;
    }

    // Typed accessors
    public double CircleRadius => Unsafe.ReadUnaligned<double>(ref _data[0]);
    public (float w, float h) Rect => (
        Unsafe.ReadUnaligned<float>(ref _data[0]),
        Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref _data[0], 4)));
    public (int x1, int y1) ColoredLineCoords => (
        Unsafe.ReadUnaligned<int>(ref _data[0]),
        Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref _data[0], 4)));
    public string? ColoredLineColor => (string?)_s0;
    public (float x, float y, float z, float w) Transform => (
        Unsafe.ReadUnaligned<float>(ref _data[0]),
        Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref _data[0], 4)),
        Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref _data[0], 8)),
        Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref _data[0], 12)));
    public double RichTextSpacing => Unsafe.ReadUnaligned<double>(ref _data[0]);
    public int RichTextSize => Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref _data[0], 8));
    public bool RichTextBold => Unsafe.ReadUnaligned<bool>(ref Unsafe.Add(ref _data[0], 12));
    public string? RichTextText => (string?)_s0;
    public string? RichTextFont => (string?)_s1;
    public string? LabelText => (string?)_s0;
    public string? ErrorMessage => (string?)_s0;
}

// ── Layout H: UnsafeOverlap (MultiLong) — long fields as byte buffer, pre-.NET 8 fallback ──
//
// Buffer = 17 bytes, rounded up to 24 (3 longs)
// Ref slots: max 2

public struct EventUnsafeLong {
    public readonly EventKind tag;

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    internal long _data0, _data1, _data2;
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    internal object? _s0, _s1;

    EventUnsafeLong(EventKind tag) { this.tag = tag; _data0 = 0; _data1 = 0; _data2 = 0; _s0 = null; _s1 = null; }

    public static EventUnsafeLong NewPoint() => new(EventKind.Point);
    public static EventUnsafeLong NewCircle(double radius) {
        var s = new EventUnsafeLong(EventKind.Circle);
        ref byte start = ref Unsafe.As<long, byte>(ref s._data0);
        Unsafe.WriteUnaligned(ref start, radius);
        return s;
    }
    public static EventUnsafeLong NewLabel(string text) {
        var s = new EventUnsafeLong(EventKind.Label);
        s._s0 = text;
        return s;
    }
    public static EventUnsafeLong NewRectangle(float w, float h) {
        var s = new EventUnsafeLong(EventKind.Rectangle);
        ref byte start = ref Unsafe.As<long, byte>(ref s._data0);
        Unsafe.WriteUnaligned(ref start, w);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref start, 4), h);
        return s;
    }
    public static EventUnsafeLong NewColoredLine(int x1, int y1, string color) {
        var s = new EventUnsafeLong(EventKind.ColoredLine);
        ref byte start = ref Unsafe.As<long, byte>(ref s._data0);
        Unsafe.WriteUnaligned(ref start, x1);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref start, 4), y1);
        s._s0 = color;
        return s;
    }
    public static EventUnsafeLong NewTransform(float x, float y, float z, float w) {
        var s = new EventUnsafeLong(EventKind.Transform);
        ref byte start = ref Unsafe.As<long, byte>(ref s._data0);
        Unsafe.WriteUnaligned(ref start, x);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref start, 4), y);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref start, 8), z);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref start, 12), w);
        return s;
    }
    public static EventUnsafeLong NewRichText(string text, int size, bool bold, string font, double spacing) {
        var s = new EventUnsafeLong(EventKind.RichText);
        ref byte start = ref Unsafe.As<long, byte>(ref s._data0);
        Unsafe.WriteUnaligned(ref start, spacing);              // 8 bytes at offset 0
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref start, 8), size);    // 4 bytes at offset 8
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref start, 12), bold);   // 1 byte at offset 12
        s._s0 = text;
        s._s1 = font;
        return s;
    }
    public static EventUnsafeLong NewError(string message) {
        var s = new EventUnsafeLong(EventKind.Error);
        s._s0 = message;
        return s;
    }

    // Typed accessors
    public double CircleRadius {
        get { ref byte start = ref Unsafe.As<long, byte>(ref _data0); return Unsafe.ReadUnaligned<double>(ref start); }
    }
    public (float w, float h) Rect {
        get {
            ref byte start = ref Unsafe.As<long, byte>(ref _data0);
            return (Unsafe.ReadUnaligned<float>(ref start),
                    Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref start, 4)));
        }
    }
    public (int x1, int y1) ColoredLineCoords {
        get {
            ref byte start = ref Unsafe.As<long, byte>(ref _data0);
            return (Unsafe.ReadUnaligned<int>(ref start),
                    Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref start, 4)));
        }
    }
    public string? ColoredLineColor => (string?)_s0;
    public (float x, float y, float z, float w) Transform {
        get {
            ref byte start = ref Unsafe.As<long, byte>(ref _data0);
            return (Unsafe.ReadUnaligned<float>(ref start),
                    Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref start, 4)),
                    Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref start, 8)),
                    Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref start, 12)));
        }
    }
    public double RichTextSpacing {
        get { ref byte start = ref Unsafe.As<long, byte>(ref _data0); return Unsafe.ReadUnaligned<double>(ref start); }
    }
    public int RichTextSize {
        get { ref byte start = ref Unsafe.As<long, byte>(ref _data0); return Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref start, 8)); }
    }
    public bool RichTextBold {
        get { ref byte start = ref Unsafe.As<long, byte>(ref _data0); return Unsafe.ReadUnaligned<bool>(ref Unsafe.Add(ref start, 12)); }
    }
    public string? RichTextText => (string?)_s0;
    public string? RichTextFont => (string?)_s1;
    public string? LabelText => (string?)_s0;
    public string? ErrorMessage => (string?)_s0;
}

// ── Helper to build random events ──

public static class EventFactory {
    public static void Fill(Random rng, int n,
        EventMultiObj[] multiObj, EventTupleObj[] tupleObj,
        EventClass[] cls, EventRecord[] rec,
        EventHybrid[]? hybrid = null,
        EventAdditive[]? additive = null,
        EventUnsafeInline[]? unsafeInline = null,
        EventUnsafeLong[]? unsafeLong = null) {

        string[] strings = ["hello", "world", "red", "blue", "Arial", "Mono", "error!", "warn"];
        for (int i = 0; i < n; i++) {
            int variant = rng.Next(8);
            switch (variant) {
                case 0:
                    multiObj[i] = EventMultiObj.NewPoint();
                    tupleObj[i] = EventTupleObj.NewPoint();
                    cls[i] = EventClass.NewPoint();
                    rec[i] = EventRecord.NewPoint();
                    if (hybrid != null) hybrid[i] = EventHybrid.NewPoint();
                    if (additive != null) additive[i] = EventAdditive.NewPoint();
                    if (unsafeInline != null) unsafeInline[i] = EventUnsafeInline.NewPoint();
                    if (unsafeLong != null) unsafeLong[i] = EventUnsafeLong.NewPoint();
                    break;
                case 1:
                    double r = rng.NextDouble() * 100;
                    multiObj[i] = EventMultiObj.NewCircle(r);
                    tupleObj[i] = EventTupleObj.NewCircle(r);
                    cls[i] = EventClass.NewCircle(r);
                    rec[i] = EventRecord.NewCircle(r);
                    if (hybrid != null) hybrid[i] = EventHybrid.NewCircle(r);
                    if (additive != null) additive[i] = EventAdditive.NewCircle(r);
                    if (unsafeInline != null) unsafeInline[i] = EventUnsafeInline.NewCircle(r);
                    if (unsafeLong != null) unsafeLong[i] = EventUnsafeLong.NewCircle(r);
                    break;
                case 2:
                    string lbl = strings[rng.Next(strings.Length)];
                    multiObj[i] = EventMultiObj.NewLabel(lbl);
                    tupleObj[i] = EventTupleObj.NewLabel(lbl);
                    cls[i] = EventClass.NewLabel(lbl);
                    rec[i] = EventRecord.NewLabel(lbl);
                    if (hybrid != null) hybrid[i] = EventHybrid.NewLabel(lbl);
                    if (additive != null) additive[i] = EventAdditive.NewLabel(lbl);
                    if (unsafeInline != null) unsafeInline[i] = EventUnsafeInline.NewLabel(lbl);
                    if (unsafeLong != null) unsafeLong[i] = EventUnsafeLong.NewLabel(lbl);
                    break;
                case 3:
                    float rw = rng.NextSingle() * 50, rh = rng.NextSingle() * 50;
                    multiObj[i] = EventMultiObj.NewRectangle(rw, rh);
                    tupleObj[i] = EventTupleObj.NewRectangle(rw, rh);
                    cls[i] = EventClass.NewRectangle(rw, rh);
                    rec[i] = EventRecord.NewRectangle(rw, rh);
                    if (hybrid != null) hybrid[i] = EventHybrid.NewRectangle(rw, rh);
                    if (additive != null) additive[i] = EventAdditive.NewRectangle(rw, rh);
                    if (unsafeInline != null) unsafeInline[i] = EventUnsafeInline.NewRectangle(rw, rh);
                    if (unsafeLong != null) unsafeLong[i] = EventUnsafeLong.NewRectangle(rw, rh);
                    break;
                case 4:
                    int x1 = rng.Next(1000), y1 = rng.Next(1000);
                    string col = strings[rng.Next(strings.Length)];
                    multiObj[i] = EventMultiObj.NewColoredLine(x1, y1, col);
                    tupleObj[i] = EventTupleObj.NewColoredLine(x1, y1, col);
                    cls[i] = EventClass.NewColoredLine(x1, y1, col);
                    rec[i] = EventRecord.NewColoredLine(x1, y1, col);
                    if (hybrid != null) hybrid[i] = EventHybrid.NewColoredLine(x1, y1, col);
                    if (additive != null) additive[i] = EventAdditive.NewColoredLine(x1, y1, col);
                    if (unsafeInline != null) unsafeInline[i] = EventUnsafeInline.NewColoredLine(x1, y1, col);
                    if (unsafeLong != null) unsafeLong[i] = EventUnsafeLong.NewColoredLine(x1, y1, col);
                    break;
                case 5:
                    float tx = rng.NextSingle(), ty = rng.NextSingle();
                    float tz = rng.NextSingle(), tw = rng.NextSingle();
                    multiObj[i] = EventMultiObj.NewTransform(tx, ty, tz, tw);
                    tupleObj[i] = EventTupleObj.NewTransform(tx, ty, tz, tw);
                    cls[i] = EventClass.NewTransform(tx, ty, tz, tw);
                    rec[i] = EventRecord.NewTransform(tx, ty, tz, tw);
                    if (hybrid != null) hybrid[i] = EventHybrid.NewTransform(tx, ty, tz, tw);
                    if (additive != null) additive[i] = EventAdditive.NewTransform(tx, ty, tz, tw);
                    if (unsafeInline != null) unsafeInline[i] = EventUnsafeInline.NewTransform(tx, ty, tz, tw);
                    if (unsafeLong != null) unsafeLong[i] = EventUnsafeLong.NewTransform(tx, ty, tz, tw);
                    break;
                case 6:
                    string txt = strings[rng.Next(strings.Length)];
                    int sz = rng.Next(8, 72);
                    bool bold = rng.Next(2) == 1;
                    string fnt = strings[rng.Next(strings.Length)];
                    double sp = rng.NextDouble() * 3;
                    multiObj[i] = EventMultiObj.NewRichText(txt, sz, bold, fnt, sp);
                    tupleObj[i] = EventTupleObj.NewRichText(txt, sz, bold, fnt, sp);
                    cls[i] = EventClass.NewRichText(txt, sz, bold, fnt, sp);
                    rec[i] = EventRecord.NewRichText(txt, sz, bold, fnt, sp);
                    if (hybrid != null) hybrid[i] = EventHybrid.NewRichText(txt, sz, bold, fnt, sp);
                    if (additive != null) additive[i] = EventAdditive.NewRichText(txt, sz, bold, fnt, sp);
                    if (unsafeInline != null) unsafeInline[i] = EventUnsafeInline.NewRichText(txt, sz, bold, fnt, sp);
                    if (unsafeLong != null) unsafeLong[i] = EventUnsafeLong.NewRichText(txt, sz, bold, fnt, sp);
                    break;
                case 7:
                    string msg = strings[rng.Next(strings.Length)];
                    multiObj[i] = EventMultiObj.NewError(msg);
                    tupleObj[i] = EventTupleObj.NewError(msg);
                    cls[i] = EventClass.NewError(msg);
                    rec[i] = EventRecord.NewError(msg);
                    if (hybrid != null) hybrid[i] = EventHybrid.NewError(msg);
                    if (additive != null) additive[i] = EventAdditive.NewError(msg);
                    if (unsafeInline != null) unsafeInline[i] = EventUnsafeInline.NewError(msg);
                    if (unsafeLong != null) unsafeLong[i] = EventUnsafeLong.NewError(msg);
                    break;
            }
        }
    }
}
