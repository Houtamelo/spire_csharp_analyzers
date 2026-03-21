using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace Spire.Benchmarks;

/// Simulates an ECS-style update loop: iterate array, match variant, compute per-element, accumulate.
[Config(typeof(SpireConfig))]
public class UpdateLoopBenchmarks
{
    [Params(BenchN.Default)]
    public int N { get; set; }

    [ParamsAllValues]
    public Distribution Dist { get; set; }

    EventAdditive[] _additive = null!;
    EventBoxedFields[] _boxedFields = null!;
    EventBoxedTuple[] _boxedTuple = null!;
    EventOverlap[] _overlap = null!;
    EventUnsafeOverlap[] _unsafeOverlap = null!;
    EventRecord[] _record = null!;
    EventClass[] _class = null!;

    [GlobalSetup]
    public void Setup()
    {
        _additive = new EventAdditive[N];
        _boxedFields = new EventBoxedFields[N];
        _boxedTuple = new EventBoxedTuple[N];
        _overlap = new EventOverlap[N];
        _unsafeOverlap = new EventUnsafeOverlap[N];
        _record = new EventRecord[N];
        _class = new EventClass[N];

        ArrayFiller.Fill(_additive, new Random(42), Dist);
        ArrayFiller.Fill(_boxedFields, new Random(42), Dist);
        ArrayFiller.Fill(_boxedTuple, new Random(42), Dist);
        ArrayFiller.Fill(_overlap, new Random(42), Dist);
        ArrayFiller.Fill(_unsafeOverlap, new Random(42), Dist);
        ArrayFiller.Fill(_record, new Random(42), Dist);
        ArrayFiller.Fill(_class, new Random(42), Dist);
    }

    // ════════════════════════════════════════════════════════════════
    // Deconstruct — uses the generated Deconstruct overloads (public API)
    //
    // All struct strategies share the same Deconstruct signatures:
    //   (Kind, object?)                                    — Circle, Label, Error (1-field generic)
    //   (Kind, float, float)                               — Rectangle
    //   (Kind, int, int, string)                           — ColoredLine
    //   (Kind, float, float, float, float)                 — Transform
    //   (Kind, string, int, bool, string, double)          — RichText
    // ════════════════════════════════════════════════════════════════

    [BenchmarkCategory("Deconstruct"), Benchmark(Baseline = true, Description = "additive")]
    public double DeconstructAdditive()
    {
        double sum = 0;
        var arr = _additive;
        for (int i = 0; i < arr.Length; i++)
        {
            var e = arr[i];
            switch (e.tag)
            {
                case EventAdditive.Kind.Circle:
                    e.Deconstruct(out _, out object? cf0);
                    sum += (double)cf0! * (double)cf0! * 3.14159;
                    break;
                case EventAdditive.Kind.Rectangle:
                    e.Deconstruct(out _, out float rw, out float rh);
                    sum += rw * rh;
                    break;
                case EventAdditive.Kind.Transform:
                    e.Deconstruct(out _, out float tx, out float ty, out float tz, out float tw);
                    sum += tx * ty + tz * tw;
                    break;
                case EventAdditive.Kind.RichText:
                    e.Deconstruct(out _, out string _, out int rsz, out bool _, out string _, out double rsp);
                    sum += rsp * rsz;
                    break;
                case EventAdditive.Kind.ColoredLine:
                    e.Deconstruct(out _, out int cx, out int cy, out string _);
                    sum += cx + cy;
                    break;
            }
        }
        return sum;
    }

    [BenchmarkCategory("Deconstruct"), Benchmark(Description = "boxedFields")]
    public double DeconstructBoxedFields()
    {
        double sum = 0;
        var arr = _boxedFields;
        for (int i = 0; i < arr.Length; i++)
        {
            var e = arr[i];
            switch (e.tag)
            {
                case EventBoxedFields.Kind.Circle:
                    e.Deconstruct(out _, out object? cf0);
                    sum += (double)cf0! * (double)cf0! * 3.14159;
                    break;
                case EventBoxedFields.Kind.Rectangle:
                    e.Deconstruct(out _, out float rw, out float rh);
                    sum += rw * rh;
                    break;
                case EventBoxedFields.Kind.Transform:
                    e.Deconstruct(out _, out float tx, out float ty, out float tz, out float tw);
                    sum += tx * ty + tz * tw;
                    break;
                case EventBoxedFields.Kind.RichText:
                    e.Deconstruct(out _, out string _, out int rsz, out bool _, out string _, out double rsp);
                    sum += rsp * rsz;
                    break;
                case EventBoxedFields.Kind.ColoredLine:
                    e.Deconstruct(out _, out int cx, out int cy, out string _);
                    sum += cx + cy;
                    break;
            }
        }
        return sum;
    }

    [BenchmarkCategory("Deconstruct"), Benchmark(Description = "boxedTuple")]
    public double DeconstructBoxedTuple()
    {
        double sum = 0;
        var arr = _boxedTuple;
        for (int i = 0; i < arr.Length; i++)
        {
            var e = arr[i];
            switch (e.tag)
            {
                case EventBoxedTuple.Kind.Circle:
                    e.Deconstruct(out _, out object? cf0);
                    sum += (double)cf0! * (double)cf0! * 3.14159;
                    break;
                case EventBoxedTuple.Kind.Rectangle:
                    // BoxedTuple only has (Kind, object?) Deconstruct — must cast payload
                    e.Deconstruct(out _, out object? rpay);
                    var rect = ((float, float))rpay!;
                    sum += rect.Item1 * rect.Item2;
                    break;
                case EventBoxedTuple.Kind.Transform:
                    e.Deconstruct(out _, out object? tpay);
                    var tf = ((float, float, float, float))tpay!;
                    sum += tf.Item1 * tf.Item2 + tf.Item3 * tf.Item4;
                    break;
                case EventBoxedTuple.Kind.RichText:
                    e.Deconstruct(out _, out object? rtpay);
                    var rt = ((string, int, bool, string, double))rtpay!;
                    sum += rt.Item5 * rt.Item2;
                    break;
                case EventBoxedTuple.Kind.ColoredLine:
                    e.Deconstruct(out _, out object? clpay);
                    var cl = ((int, int, string))clpay!;
                    sum += cl.Item1 + cl.Item2;
                    break;
            }
        }
        return sum;
    }

    [BenchmarkCategory("Deconstruct"), Benchmark(Description = "overlap")]
    public double DeconstructOverlap()
    {
        double sum = 0;
        var arr = _overlap;
        for (int i = 0; i < arr.Length; i++)
        {
            var e = arr[i];
            switch (e.tag)
            {
                case EventOverlap.Kind.Circle:
                    e.Deconstruct(out _, out object? cf0);
                    sum += (double)cf0! * (double)cf0! * 3.14159;
                    break;
                case EventOverlap.Kind.Rectangle:
                    e.Deconstruct(out _, out float rw, out float rh);
                    sum += rw * rh;
                    break;
                case EventOverlap.Kind.Transform:
                    e.Deconstruct(out _, out float tx, out float ty, out float tz, out float tw);
                    sum += tx * ty + tz * tw;
                    break;
                case EventOverlap.Kind.RichText:
                    e.Deconstruct(out _, out string _, out int rsz, out bool _, out string _, out double rsp);
                    sum += rsp * rsz;
                    break;
                case EventOverlap.Kind.ColoredLine:
                    e.Deconstruct(out _, out int cx, out int cy, out string _);
                    sum += cx + cy;
                    break;
            }
        }
        return sum;
    }

    [BenchmarkCategory("Deconstruct"), Benchmark(Description = "unsafeOverlap")]
    public double DeconstructUnsafeOverlap()
    {
        double sum = 0;
        var arr = _unsafeOverlap;
        for (int i = 0; i < arr.Length; i++)
        {
            var e = arr[i];
            switch (e.tag)
            {
                case EventUnsafeOverlap.Kind.Circle:
                    e.Deconstruct(out _, out object? cf0);
                    sum += (double)cf0! * (double)cf0! * 3.14159;
                    break;
                case EventUnsafeOverlap.Kind.Rectangle:
                    e.Deconstruct(out _, out float rw, out float rh);
                    sum += rw * rh;
                    break;
                case EventUnsafeOverlap.Kind.Transform:
                    e.Deconstruct(out _, out float tx, out float ty, out float tz, out float tw);
                    sum += tx * ty + tz * tw;
                    break;
                case EventUnsafeOverlap.Kind.RichText:
                    e.Deconstruct(out _, out string _, out int rsz, out bool _, out string _, out double rsp);
                    sum += rsp * rsz;
                    break;
                case EventUnsafeOverlap.Kind.ColoredLine:
                    e.Deconstruct(out _, out int cx, out int cy, out string _);
                    sum += cx + cy;
                    break;
            }
        }
        return sum;
    }

    [BenchmarkCategory("Deconstruct"), Benchmark(Description = "record")]
    public double DeconstructRecord()
    {
        double sum = 0;
        var arr = _record;
        for (int i = 0; i < arr.Length; i++)
        {
            sum += arr[i] switch
            {
                EventRecord.Circle(var radius) => radius * radius * 3.14159,
                EventRecord.Rectangle(var w, var h) => w * h,
                EventRecord.Transform(var x, var y, var z, var w) => x * y + z * w,
                EventRecord.RichText(_, var sz, _, _, var sp) => sp * sz,
                EventRecord.ColoredLine(var x1, var y1, _) => x1 + y1,
                _ => 0,
            };
        }
        return sum;
    }

    [BenchmarkCategory("Deconstruct"), Benchmark(Description = "class")]
    public double DeconstructClass()
    {
        double sum = 0;
        var arr = _class;
        for (int i = 0; i < arr.Length; i++)
        {
            // Class variants don't have positional Deconstruct — use type pattern + properties
            sum += arr[i] switch
            {
                EventClass.Circle c => c.Radius * c.Radius * 3.14159,
                EventClass.Rectangle r => r.Width * r.Height,
                EventClass.Transform t => t.X * t.Y + t.Z * t.W,
                EventClass.RichText rt => rt.Spacing * rt.Size,
                EventClass.ColoredLine cl => cl.X1 + cl.Y1,
                _ => 0,
            };
        }
        return sum;
    }

    // ════════════════════════════════════════════════════════════════
    // Property — C# property pattern syntax: case { tag: Kind.X, field: var x }
    //
    // All struct strategies now have public typed properties.
    // Record/Class: type + property pattern.
    // ════════════════════════════════════════════════════════════════

    [BenchmarkCategory("Property"), Benchmark(Baseline = true, Description = "additive")]
    public double PropertyAdditive()
    {
        double sum = 0;
        var arr = _additive;
        for (int i = 0; i < arr.Length; i++)
        {
            switch (arr[i])
            {
                case { tag: EventAdditive.Kind.Circle, radius: var r }:
                    sum += r * r * 3.14159;
                    break;
                case { tag: EventAdditive.Kind.Rectangle, width: var w, height: var h }:
                    sum += w * h;
                    break;
                case { tag: EventAdditive.Kind.Transform, x: var x, y: var y, z: var z, w: var w }:
                    sum += x * y + z * w;
                    break;
                case { tag: EventAdditive.Kind.RichText, spacing: var sp, size: var sz }:
                    sum += sp * sz;
                    break;
                case { tag: EventAdditive.Kind.ColoredLine, x1: var x1, y1: var y1 }:
                    sum += x1 + y1;
                    break;
            }
        }
        return sum;
    }

    [BenchmarkCategory("Property"), Benchmark(Description = "boxedFields")]
    public double PropertyBoxedFields()
    {
        double sum = 0;
        var arr = _boxedFields;
        for (int i = 0; i < arr.Length; i++)
        {
            switch (arr[i])
            {
                case { tag: EventBoxedFields.Kind.Circle, radius: var r }:
                    sum += r * r * 3.14159;
                    break;
                case { tag: EventBoxedFields.Kind.Rectangle, width: var w, height: var h }:
                    sum += w * h;
                    break;
                case { tag: EventBoxedFields.Kind.Transform, x: var x, y: var y, z: var z, w: var w }:
                    sum += x * y + z * w;
                    break;
                case { tag: EventBoxedFields.Kind.RichText, spacing: var sp, size: var sz }:
                    sum += sp * sz;
                    break;
                case { tag: EventBoxedFields.Kind.ColoredLine, x1: var x1, y1: var y1 }:
                    sum += x1 + y1;
                    break;
            }
        }
        return sum;
    }

    [BenchmarkCategory("Property"), Benchmark(Description = "boxedTuple")]
    public double PropertyBoxedTuple()
    {
        double sum = 0;
        var arr = _boxedTuple;
        for (int i = 0; i < arr.Length; i++)
        {
            switch (arr[i])
            {
                case { tag: EventBoxedTuple.Kind.Circle, radius: var r }:
                    sum += r * r * 3.14159;
                    break;
                case { tag: EventBoxedTuple.Kind.Rectangle, width: var w, height: var h }:
                    sum += w * h;
                    break;
                case { tag: EventBoxedTuple.Kind.Transform, x: var x, y: var y, z: var z, w: var w }:
                    sum += x * y + z * w;
                    break;
                case { tag: EventBoxedTuple.Kind.RichText, spacing: var sp, size: var sz }:
                    sum += sp * sz;
                    break;
                case { tag: EventBoxedTuple.Kind.ColoredLine, x1: var x1, y1: var y1 }:
                    sum += x1 + y1;
                    break;
            }
        }
        return sum;
    }

    [BenchmarkCategory("Property"), Benchmark(Description = "overlap")]
    public double PropertyOverlap()
    {
        double sum = 0;
        var arr = _overlap;
        for (int i = 0; i < arr.Length; i++)
        {
            switch (arr[i])
            {
                case { tag: EventOverlap.Kind.Circle, radius: var r }:
                    sum += r * r * 3.14159;
                    break;
                case { tag: EventOverlap.Kind.Rectangle, width: var w, height: var h }:
                    sum += w * h;
                    break;
                case { tag: EventOverlap.Kind.Transform, x: var x, y: var y, z: var z, w: var w }:
                    sum += x * y + z * w;
                    break;
                case { tag: EventOverlap.Kind.RichText, spacing: var sp, size: var sz }:
                    sum += sp * sz;
                    break;
                case { tag: EventOverlap.Kind.ColoredLine, x1: var x1, y1: var y1 }:
                    sum += x1 + y1;
                    break;
            }
        }
        return sum;
    }

    [BenchmarkCategory("Property"), Benchmark(Description = "unsafeOverlap")]
    public double PropertyUnsafeOverlap()
    {
        double sum = 0;
        var arr = _unsafeOverlap;
        for (int i = 0; i < arr.Length; i++)
        {
            switch (arr[i])
            {
                case { tag: EventUnsafeOverlap.Kind.Circle, radius: var r }:
                    sum += r * r * 3.14159;
                    break;
                case { tag: EventUnsafeOverlap.Kind.Rectangle, width: var w, height: var h }:
                    sum += w * h;
                    break;
                case { tag: EventUnsafeOverlap.Kind.Transform, x: var x, y: var y, z: var z, w: var w }:
                    sum += x * y + z * w;
                    break;
                case { tag: EventUnsafeOverlap.Kind.RichText, spacing: var sp, size: var sz }:
                    sum += sp * sz;
                    break;
                case { tag: EventUnsafeOverlap.Kind.ColoredLine, x1: var x1, y1: var y1 }:
                    sum += x1 + y1;
                    break;
            }
        }
        return sum;
    }

    [BenchmarkCategory("Property"), Benchmark(Description = "record")]
    public double PropertyRecord()
    {
        double sum = 0;
        var arr = _record;
        for (int i = 0; i < arr.Length; i++)
        {
            switch (arr[i])
            {
                case EventRecord.Circle { Radius: var r }:
                    sum += r * r * 3.14159;
                    break;
                case EventRecord.Rectangle { Width: var w, Height: var h }:
                    sum += w * h;
                    break;
                case EventRecord.Transform { X: var x, Y: var y, Z: var z, W: var w }:
                    sum += x * y + z * w;
                    break;
                case EventRecord.RichText { Spacing: var sp, Size: var sz }:
                    sum += sp * sz;
                    break;
                case EventRecord.ColoredLine { X1: var x1, Y1: var y1 }:
                    sum += x1 + y1;
                    break;
            }
        }
        return sum;
    }

    [BenchmarkCategory("Property"), Benchmark(Description = "class")]
    public double PropertyClass()
    {
        double sum = 0;
        var arr = _class;
        for (int i = 0; i < arr.Length; i++)
        {
            switch (arr[i])
            {
                case EventClass.Circle { Radius: var r }:
                    sum += r * r * 3.14159;
                    break;
                case EventClass.Rectangle { Width: var w, Height: var h }:
                    sum += w * h;
                    break;
                case EventClass.Transform { X: var x, Y: var y, Z: var z, W: var w }:
                    sum += x * y + z * w;
                    break;
                case EventClass.RichText { Spacing: var sp, Size: var sz }:
                    sum += sp * sz;
                    break;
                case EventClass.ColoredLine { X1: var x1, Y1: var y1 }:
                    sum += x1 + y1;
                    break;
            }
        }
        return sum;
    }
}
