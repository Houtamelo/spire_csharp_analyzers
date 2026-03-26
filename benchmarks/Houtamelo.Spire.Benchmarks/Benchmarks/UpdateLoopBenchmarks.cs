using BenchmarkDotNet.Attributes;
using Houtamelo.Spire.Benchmarks.Helpers;

namespace Houtamelo.Spire.Benchmarks.Benchmarks;

/// Simulates an ECS-style update loop: iterate array, match variant, compute per-element, accumulate.
[Config(typeof(SpireConfig))]
public class UpdateLoopBenchmarks
{
    [Params(BenchN.Default)]
    public int N { get; set; }

    [ParamsAllValues]
    public Distribution Dist { get; set; }

    Types.EventAdditive[] _additive = null!;
    Types.EventBoxedFields[] _boxedFields = null!;
    Types.EventBoxedTuple[] _boxedTuple = null!;
    Types.EventOverlap[] _overlap = null!;
    Types.EventUnsafeOverlap[] _unsafeOverlap = null!;
    Types.EventRecord[] _record = null!;
    Types.EventClass[] _class = null!;

    [GlobalSetup]
    public void Setup()
    {
        _additive = new Types.EventAdditive[N];
        _boxedFields = new Types.EventBoxedFields[N];
        _boxedTuple = new Types.EventBoxedTuple[N];
        _overlap = new Types.EventOverlap[N];
        _unsafeOverlap = new Types.EventUnsafeOverlap[N];
        _record = new Types.EventRecord[N];
        _class = new Types.EventClass[N];

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
            switch (e.kind)
            {
                case Types.EventAdditive.Kind.Circle:
                    e.Deconstruct(out _, out object? cf0);
                    sum += (double)cf0! * (double)cf0! * 3.14159;
                    break;
                case Types.EventAdditive.Kind.Rectangle:
                    e.Deconstruct(out _, out float rw, out float rh);
                    sum += rw * rh;
                    break;
                case Types.EventAdditive.Kind.Transform:
                    e.Deconstruct(out _, out float tx, out float ty, out float tz, out float tw);
                    sum += tx * ty + tz * tw;
                    break;
                case Types.EventAdditive.Kind.RichText:
                    e.Deconstruct(out _, out string _, out int rsz, out bool _, out string _, out double rsp);
                    sum += rsp * rsz;
                    break;
                case Types.EventAdditive.Kind.ColoredLine:
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
            switch (e.kind)
            {
                case Types.EventBoxedFields.Kind.Circle:
                    e.Deconstruct(out _, out object? cf0);
                    sum += (double)cf0! * (double)cf0! * 3.14159;
                    break;
                case Types.EventBoxedFields.Kind.Rectangle:
                    e.Deconstruct(out _, out float rw, out float rh);
                    sum += rw * rh;
                    break;
                case Types.EventBoxedFields.Kind.Transform:
                    e.Deconstruct(out _, out float tx, out float ty, out float tz, out float tw);
                    sum += tx * ty + tz * tw;
                    break;
                case Types.EventBoxedFields.Kind.RichText:
                    e.Deconstruct(out _, out string _, out int rsz, out bool _, out string _, out double rsp);
                    sum += rsp * rsz;
                    break;
                case Types.EventBoxedFields.Kind.ColoredLine:
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
            switch (e.kind)
            {
                case Types.EventBoxedTuple.Kind.Circle:
                    e.Deconstruct(out _, out object? cf0);
                    sum += (double)cf0! * (double)cf0! * 3.14159;
                    break;
                case Types.EventBoxedTuple.Kind.Rectangle:
                    // BoxedTuple only has (Kind, object?) Deconstruct — must cast payload
                    e.Deconstruct(out _, out object? rpay);
                    var rect = ((float, float))rpay!;
                    sum += rect.Item1 * rect.Item2;
                    break;
                case Types.EventBoxedTuple.Kind.Transform:
                    e.Deconstruct(out _, out object? tpay);
                    var tf = ((float, float, float, float))tpay!;
                    sum += tf.Item1 * tf.Item2 + tf.Item3 * tf.Item4;
                    break;
                case Types.EventBoxedTuple.Kind.RichText:
                    e.Deconstruct(out _, out object? rtpay);
                    var rt = ((string, int, bool, string, double))rtpay!;
                    sum += rt.Item5 * rt.Item2;
                    break;
                case Types.EventBoxedTuple.Kind.ColoredLine:
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
            switch (e.kind)
            {
                case Types.EventOverlap.Kind.Circle:
                    e.Deconstruct(out _, out object? cf0);
                    sum += (double)cf0! * (double)cf0! * 3.14159;
                    break;
                case Types.EventOverlap.Kind.Rectangle:
                    e.Deconstruct(out _, out float rw, out float rh);
                    sum += rw * rh;
                    break;
                case Types.EventOverlap.Kind.Transform:
                    e.Deconstruct(out _, out float tx, out float ty, out float tz, out float tw);
                    sum += tx * ty + tz * tw;
                    break;
                case Types.EventOverlap.Kind.RichText:
                    e.Deconstruct(out _, out string _, out int rsz, out bool _, out string _, out double rsp);
                    sum += rsp * rsz;
                    break;
                case Types.EventOverlap.Kind.ColoredLine:
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
            switch (e.kind)
            {
                case Types.EventUnsafeOverlap.Kind.Circle:
                    e.Deconstruct(out _, out object? cf0);
                    sum += (double)cf0! * (double)cf0! * 3.14159;
                    break;
                case Types.EventUnsafeOverlap.Kind.Rectangle:
                    e.Deconstruct(out _, out float rw, out float rh);
                    sum += rw * rh;
                    break;
                case Types.EventUnsafeOverlap.Kind.Transform:
                    e.Deconstruct(out _, out float tx, out float ty, out float tz, out float tw);
                    sum += tx * ty + tz * tw;
                    break;
                case Types.EventUnsafeOverlap.Kind.RichText:
                    e.Deconstruct(out _, out string _, out int rsz, out bool _, out string _, out double rsp);
                    sum += rsp * rsz;
                    break;
                case Types.EventUnsafeOverlap.Kind.ColoredLine:
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
                Types.EventRecord.Circle(var radius) => radius * radius * 3.14159,
                Types.EventRecord.Rectangle(var w, var h) => w * h,
                Types.EventRecord.Transform(var x, var y, var z, var w) => x * y + z * w,
                Types.EventRecord.RichText(_, var sz, _, _, var sp) => sp * sz,
                Types.EventRecord.ColoredLine(var x1, var y1, _) => x1 + y1,
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
                Types.EventClass.Circle c => c.Radius * c.Radius * 3.14159,
                Types.EventClass.Rectangle r => r.Width * r.Height,
                Types.EventClass.Transform t => t.X * t.Y + t.Z * t.W,
                Types.EventClass.RichText rt => rt.Spacing * rt.Size,
                Types.EventClass.ColoredLine cl => cl.X1 + cl.Y1,
                _ => 0,
            };
        }
        return sum;
    }

    // ════════════════════════════════════════════════════════════════
    // Property — C# property pattern syntax: case { kind: Kind.X, field: var x }
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
                case { kind: Types.EventAdditive.Kind.Circle, radius: var r }:
                    sum += r * r * 3.14159;
                    break;
                case { kind: Types.EventAdditive.Kind.Rectangle, width: var w, height: var h }:
                    sum += w * h;
                    break;
                case { kind: Types.EventAdditive.Kind.Transform, x: var x, y: var y, z: var z, w: var w }:
                    sum += x * y + z * w;
                    break;
                case { kind: Types.EventAdditive.Kind.RichText, spacing: var sp, size: var sz }:
                    sum += sp * sz;
                    break;
                case { kind: Types.EventAdditive.Kind.ColoredLine, x1: var x1, y1: var y1 }:
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
                case { kind: Types.EventBoxedFields.Kind.Circle, radius: var r }:
                    sum += r * r * 3.14159;
                    break;
                case { kind: Types.EventBoxedFields.Kind.Rectangle, width: var w, height: var h }:
                    sum += w * h;
                    break;
                case { kind: Types.EventBoxedFields.Kind.Transform, x: var x, y: var y, z: var z, w: var w }:
                    sum += x * y + z * w;
                    break;
                case { kind: Types.EventBoxedFields.Kind.RichText, spacing: var sp, size: var sz }:
                    sum += sp * sz;
                    break;
                case { kind: Types.EventBoxedFields.Kind.ColoredLine, x1: var x1, y1: var y1 }:
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
                case { kind: Types.EventBoxedTuple.Kind.Circle, radius: var r }:
                    sum += r * r * 3.14159;
                    break;
                case { kind: Types.EventBoxedTuple.Kind.Rectangle, width: var w, height: var h }:
                    sum += w * h;
                    break;
                case { kind: Types.EventBoxedTuple.Kind.Transform, x: var x, y: var y, z: var z, w: var w }:
                    sum += x * y + z * w;
                    break;
                case { kind: Types.EventBoxedTuple.Kind.RichText, spacing: var sp, size: var sz }:
                    sum += sp * sz;
                    break;
                case { kind: Types.EventBoxedTuple.Kind.ColoredLine, x1: var x1, y1: var y1 }:
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
                case { kind: Types.EventOverlap.Kind.Circle, radius: var r }:
                    sum += r * r * 3.14159;
                    break;
                case { kind: Types.EventOverlap.Kind.Rectangle, width: var w, height: var h }:
                    sum += w * h;
                    break;
                case { kind: Types.EventOverlap.Kind.Transform, x: var x, y: var y, z: var z, w: var w }:
                    sum += x * y + z * w;
                    break;
                case { kind: Types.EventOverlap.Kind.RichText, spacing: var sp, size: var sz }:
                    sum += sp * sz;
                    break;
                case { kind: Types.EventOverlap.Kind.ColoredLine, x1: var x1, y1: var y1 }:
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
                case { kind: Types.EventUnsafeOverlap.Kind.Circle, radius: var r }:
                    sum += r * r * 3.14159;
                    break;
                case { kind: Types.EventUnsafeOverlap.Kind.Rectangle, width: var w, height: var h }:
                    sum += w * h;
                    break;
                case { kind: Types.EventUnsafeOverlap.Kind.Transform, x: var x, y: var y, z: var z, w: var w }:
                    sum += x * y + z * w;
                    break;
                case { kind: Types.EventUnsafeOverlap.Kind.RichText, spacing: var sp, size: var sz }:
                    sum += sp * sz;
                    break;
                case { kind: Types.EventUnsafeOverlap.Kind.ColoredLine, x1: var x1, y1: var y1 }:
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
                case Types.EventRecord.Circle { Radius: var r }:
                    sum += r * r * 3.14159;
                    break;
                case Types.EventRecord.Rectangle { Width: var w, Height: var h }:
                    sum += w * h;
                    break;
                case Types.EventRecord.Transform { X: var x, Y: var y, Z: var z, W: var w }:
                    sum += x * y + z * w;
                    break;
                case Types.EventRecord.RichText { Spacing: var sp, Size: var sz }:
                    sum += sp * sz;
                    break;
                case Types.EventRecord.ColoredLine { X1: var x1, Y1: var y1 }:
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
                case Types.EventClass.Circle { Radius: var r }:
                    sum += r * r * 3.14159;
                    break;
                case Types.EventClass.Rectangle { Width: var w, Height: var h }:
                    sum += w * h;
                    break;
                case Types.EventClass.Transform { X: var x, Y: var y, Z: var z, W: var w }:
                    sum += x * y + z * w;
                    break;
                case Types.EventClass.RichText { Spacing: var sp, Size: var sz }:
                    sum += sp * sz;
                    break;
                case Types.EventClass.ColoredLine { X1: var x1, Y1: var y1 }:
                    sum += x1 + y1;
                    break;
            }
        }
        return sum;
    }
}
