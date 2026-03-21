using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace Spire.Benchmarks;

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class UnionBenchmarks {
    const int N = 1000;

    EventMultiObj[] _multiObj = null!;
    EventTupleObj[] _tupleObj = null!;
    EventHybrid[] _hybrid = null!;
    EventClass[] _class = null!;
    EventRecord[] _record = null!;
    EventAdditive[] _additive = null!;
    EventUnsafeInline[] _unsafeInline = null!;
    EventUnsafeLong[] _unsafeLong = null!;

    // Random construction inputs (pre-generated arrays)
    int[] _ints = null!;
    float[] _floats = null!;
    double[] _doubles = null!;
    string[] _strings = null!;
    bool[] _bools = null!;

    [GlobalSetup]
    public void Setup() {
        Console.WriteLine($"sizeof(EventMultiObj)      = {Unsafe.SizeOf<EventMultiObj>()}");
        Console.WriteLine($"sizeof(EventTupleObj)      = {Unsafe.SizeOf<EventTupleObj>()}");
        Console.WriteLine($"sizeof(EventHybrid)        = {Unsafe.SizeOf<EventHybrid>()}");
        Console.WriteLine($"sizeof(EventClass ref)     = {IntPtr.Size}");
        Console.WriteLine($"sizeof(EventRecord ref)    = {IntPtr.Size}");
        Console.WriteLine($"sizeof(EventAdditive)      = {Unsafe.SizeOf<EventAdditive>()}");
        Console.WriteLine($"sizeof(EventUnsafeInline)  = {Unsafe.SizeOf<EventUnsafeInline>()}");
        Console.WriteLine($"sizeof(EventUnsafeLong)    = {Unsafe.SizeOf<EventUnsafeLong>()}");

        var rng = new Random(42);

        string[] pool = ["hello", "world", "red", "blue", "Arial", "Mono", "error!", "warn"];
        _ints = new int[N];
        _floats = new float[N];
        _doubles = new double[N];
        _strings = new string[N];
        _bools = new bool[N];
        for (int i = 0; i < N; i++) {
            _ints[i] = rng.Next(-1000, 1000);
            _floats[i] = rng.NextSingle() * 200 - 100;
            _doubles[i] = rng.NextDouble() * 200 - 100;
            _strings[i] = pool[rng.Next(pool.Length)];
            _bools[i] = rng.Next(2) == 1;
        }

        _multiObj = new EventMultiObj[N];
        _tupleObj = new EventTupleObj[N];
        _hybrid = new EventHybrid[N];
        _class = new EventClass[N];
        _record = new EventRecord[N];
        _additive = new EventAdditive[N];
        _unsafeInline = new EventUnsafeInline[N];
        _unsafeLong = new EventUnsafeLong[N];

        EventFactory.Fill(new Random(42), N, _multiObj, _tupleObj, _class, _record,
            _hybrid, _additive, _unsafeInline, _unsafeLong);
    }

    // ══════════════════════════════════════════
    // Construction
    // ══════════════════════════════════════════

    [BenchmarkCategory("Construct"), Benchmark(Description = "multiobj")]
    public EventMultiObj[] ConstructMultiObj() {
        var result = new EventMultiObj[N];
        var ints = _ints; var floats = _floats; var doubles = _doubles;
        var strings = _strings; var bools = _bools;
        for (int i = 0; i < N; i++) {
            result[i] = (i % 8) switch {
                0 => EventMultiObj.NewPoint(),
                1 => EventMultiObj.NewCircle(doubles[i]),
                2 => EventMultiObj.NewLabel(strings[i]),
                3 => EventMultiObj.NewRectangle(floats[i], floats[(i + 1) % N]),
                4 => EventMultiObj.NewColoredLine(ints[i], ints[(i + 1) % N], strings[i]),
                5 => EventMultiObj.NewTransform(floats[i], floats[(i+1)%N], floats[(i+2)%N], floats[(i+3)%N]),
                6 => EventMultiObj.NewRichText(strings[i], ints[i], bools[i], strings[(i+1)%N], doubles[i]),
                _ => EventMultiObj.NewError(strings[i]),
            };
        }
        return result;
    }

    [BenchmarkCategory("Construct"), Benchmark(Description = "tupleobj")]
    public EventTupleObj[] ConstructTupleObj() {
        var result = new EventTupleObj[N];
        var ints = _ints; var floats = _floats; var doubles = _doubles;
        var strings = _strings; var bools = _bools;
        for (int i = 0; i < N; i++) {
            result[i] = (i % 8) switch {
                0 => EventTupleObj.NewPoint(),
                1 => EventTupleObj.NewCircle(doubles[i]),
                2 => EventTupleObj.NewLabel(strings[i]),
                3 => EventTupleObj.NewRectangle(floats[i], floats[(i + 1) % N]),
                4 => EventTupleObj.NewColoredLine(ints[i], ints[(i + 1) % N], strings[i]),
                5 => EventTupleObj.NewTransform(floats[i], floats[(i+1)%N], floats[(i+2)%N], floats[(i+3)%N]),
                6 => EventTupleObj.NewRichText(strings[i], ints[i], bools[i], strings[(i+1)%N], doubles[i]),
                _ => EventTupleObj.NewError(strings[i]),
            };
        }
        return result;
    }

    [BenchmarkCategory("Construct"), Benchmark(Description = "hybrid explicit")]
    public EventHybrid[] ConstructHybrid() {
        var result = new EventHybrid[N];
        var ints = _ints; var floats = _floats; var doubles = _doubles;
        var strings = _strings; var bools = _bools;
        for (int i = 0; i < N; i++) {
            result[i] = (i % 8) switch {
                0 => EventHybrid.NewPoint(),
                1 => EventHybrid.NewCircle(doubles[i]),
                2 => EventHybrid.NewLabel(strings[i]),
                3 => EventHybrid.NewRectangle(floats[i], floats[(i + 1) % N]),
                4 => EventHybrid.NewColoredLine(ints[i], ints[(i + 1) % N], strings[i]),
                5 => EventHybrid.NewTransform(floats[i], floats[(i+1)%N], floats[(i+2)%N], floats[(i+3)%N]),
                6 => EventHybrid.NewRichText(strings[i], ints[i], bools[i], strings[(i+1)%N], doubles[i]),
                _ => EventHybrid.NewError(strings[i]),
            };
        }
        return result;
    }

    [BenchmarkCategory("Construct"), Benchmark(Description = "abstract class")]
    public EventClass[] ConstructClass() {
        var result = new EventClass[N];
        var ints = _ints; var floats = _floats; var doubles = _doubles;
        var strings = _strings; var bools = _bools;
        for (int i = 0; i < N; i++) {
            result[i] = (i % 8) switch {
                0 => EventClass.NewPoint(),
                1 => EventClass.NewCircle(doubles[i]),
                2 => EventClass.NewLabel(strings[i]),
                3 => EventClass.NewRectangle(floats[i], floats[(i + 1) % N]),
                4 => EventClass.NewColoredLine(ints[i], ints[(i + 1) % N], strings[i]),
                5 => EventClass.NewTransform(floats[i], floats[(i+1)%N], floats[(i+2)%N], floats[(i+3)%N]),
                6 => EventClass.NewRichText(strings[i], ints[i], bools[i], strings[(i+1)%N], doubles[i]),
                _ => EventClass.NewError(strings[i]),
            };
        }
        return result;
    }

    [BenchmarkCategory("Construct"), Benchmark(Description = "abstract record")]
    public EventRecord[] ConstructRecord() {
        var result = new EventRecord[N];
        var ints = _ints; var floats = _floats; var doubles = _doubles;
        var strings = _strings; var bools = _bools;
        for (int i = 0; i < N; i++) {
            result[i] = (i % 8) switch {
                0 => EventRecord.NewPoint(),
                1 => EventRecord.NewCircle(doubles[i]),
                2 => EventRecord.NewLabel(strings[i]),
                3 => EventRecord.NewRectangle(floats[i], floats[(i + 1) % N]),
                4 => EventRecord.NewColoredLine(ints[i], ints[(i + 1) % N], strings[i]),
                5 => EventRecord.NewTransform(floats[i], floats[(i+1)%N], floats[(i+2)%N], floats[(i+3)%N]),
                6 => EventRecord.NewRichText(strings[i], ints[i], bools[i], strings[(i+1)%N], doubles[i]),
                _ => EventRecord.NewError(strings[i]),
            };
        }
        return result;
    }

    [BenchmarkCategory("Construct"), Benchmark(Description = "additive")]
    public EventAdditive[] ConstructAdditive() {
        var result = new EventAdditive[N];
        var ints = _ints; var floats = _floats; var doubles = _doubles;
        var strings = _strings; var bools = _bools;
        for (int i = 0; i < N; i++) {
            result[i] = (i % 8) switch {
                0 => EventAdditive.NewPoint(),
                1 => EventAdditive.NewCircle(doubles[i]),
                2 => EventAdditive.NewLabel(strings[i]),
                3 => EventAdditive.NewRectangle(floats[i], floats[(i + 1) % N]),
                4 => EventAdditive.NewColoredLine(ints[i], ints[(i + 1) % N], strings[i]),
                5 => EventAdditive.NewTransform(floats[i], floats[(i+1)%N], floats[(i+2)%N], floats[(i+3)%N]),
                6 => EventAdditive.NewRichText(strings[i], ints[i], bools[i], strings[(i+1)%N], doubles[i]),
                _ => EventAdditive.NewError(strings[i]),
            };
        }
        return result;
    }

    [BenchmarkCategory("Construct"), Benchmark(Description = "unsafe inline")]
    public EventUnsafeInline[] ConstructUnsafeInline() {
        var result = new EventUnsafeInline[N];
        var ints = _ints; var floats = _floats; var doubles = _doubles;
        var strings = _strings; var bools = _bools;
        for (int i = 0; i < N; i++) {
            result[i] = (i % 8) switch {
                0 => EventUnsafeInline.NewPoint(),
                1 => EventUnsafeInline.NewCircle(doubles[i]),
                2 => EventUnsafeInline.NewLabel(strings[i]),
                3 => EventUnsafeInline.NewRectangle(floats[i], floats[(i + 1) % N]),
                4 => EventUnsafeInline.NewColoredLine(ints[i], ints[(i + 1) % N], strings[i]),
                5 => EventUnsafeInline.NewTransform(floats[i], floats[(i+1)%N], floats[(i+2)%N], floats[(i+3)%N]),
                6 => EventUnsafeInline.NewRichText(strings[i], ints[i], bools[i], strings[(i+1)%N], doubles[i]),
                _ => EventUnsafeInline.NewError(strings[i]),
            };
        }
        return result;
    }

    [BenchmarkCategory("Construct"), Benchmark(Description = "unsafe long")]
    public EventUnsafeLong[] ConstructUnsafeLong() {
        var result = new EventUnsafeLong[N];
        var ints = _ints; var floats = _floats; var doubles = _doubles;
        var strings = _strings; var bools = _bools;
        for (int i = 0; i < N; i++) {
            result[i] = (i % 8) switch {
                0 => EventUnsafeLong.NewPoint(),
                1 => EventUnsafeLong.NewCircle(doubles[i]),
                2 => EventUnsafeLong.NewLabel(strings[i]),
                3 => EventUnsafeLong.NewRectangle(floats[i], floats[(i + 1) % N]),
                4 => EventUnsafeLong.NewColoredLine(ints[i], ints[(i + 1) % N], strings[i]),
                5 => EventUnsafeLong.NewTransform(floats[i], floats[(i+1)%N], floats[(i+2)%N], floats[(i+3)%N]),
                6 => EventUnsafeLong.NewRichText(strings[i], ints[i], bools[i], strings[(i+1)%N], doubles[i]),
                _ => EventUnsafeLong.NewError(strings[i]),
            };
        }
        return result;
    }

    // ══════════════════════════════════════════
    // Match — property pattern (typed field access, zero boxing)
    // ══════════════════════════════════════════

    [BenchmarkCategory("Match"), Benchmark(Description = "hybrid explicit (property)")]
    public double MatchHybridProperty() {
        double sum = 0;
        var arr = _hybrid;
        for (int i = 0; i < arr.Length; i++) {
            ref readonly var e = ref arr[i];
            sum += e.tag switch {
                EventKind.Point => 0,
                EventKind.Circle => e.circle_radius,
                EventKind.Label => e.ref_0!.Length,
                EventKind.Rectangle => e.rect_width * e.rect_height,
                EventKind.ColoredLine => e.coloredline_x1 + e.coloredline_y1,
                EventKind.Transform => e.transform_x + e.transform_y + e.transform_z + e.transform_w,
                EventKind.RichText => e.ref_0!.Length + e.richtext_size + e.richtext_spacing,
                EventKind.Error => e.ref_0!.Length,
                _ => 0,
            };
        }
        return sum;
    }

    [BenchmarkCategory("Match"), Benchmark(Description = "additive (property)")]
    public double MatchAdditiveProperty() {
        double sum = 0;
        var arr = _additive;
        for (int i = 0; i < arr.Length; i++) {
            ref var e = ref arr[i];
            sum += e.tag switch {
                EventKind.Point => 0,
                EventKind.Circle => e.CircleRadius,
                EventKind.Label => e.LabelText!.Length,
                EventKind.Rectangle => e.Rect.w * e.Rect.h,
                EventKind.ColoredLine => e.ColoredLineCoords.x1 + e.ColoredLineCoords.y1,
                EventKind.Transform => e.Transform.x + e.Transform.y + e.Transform.z + e.Transform.w,
                EventKind.RichText => e.RichTextText!.Length + e.RichTextSize + e.RichTextSpacing,
                EventKind.Error => e.ErrorMessage!.Length,
                _ => 0,
            };
        }
        return sum;
    }

    [BenchmarkCategory("Match"), Benchmark(Description = "unsafe inline (property)")]
    public double MatchUnsafeInlineProperty() {
        double sum = 0;
        var arr = _unsafeInline;
        for (int i = 0; i < arr.Length; i++) {
            ref var e = ref arr[i];
            sum += e.tag switch {
                EventKind.Point => 0,
                EventKind.Circle => e.CircleRadius,
                EventKind.Label => e.LabelText!.Length,
                EventKind.Rectangle => e.Rect.w * e.Rect.h,
                EventKind.ColoredLine => e.ColoredLineCoords.x1 + e.ColoredLineCoords.y1,
                EventKind.Transform => e.Transform.x + e.Transform.y + e.Transform.z + e.Transform.w,
                EventKind.RichText => e.RichTextText!.Length + e.RichTextSize + e.RichTextSpacing,
                EventKind.Error => e.ErrorMessage!.Length,
                _ => 0,
            };
        }
        return sum;
    }

    [BenchmarkCategory("Match"), Benchmark(Description = "unsafe long (property)")]
    public double MatchUnsafeLongProperty() {
        double sum = 0;
        var arr = _unsafeLong;
        for (int i = 0; i < arr.Length; i++) {
            ref var e = ref arr[i];
            sum += e.tag switch {
                EventKind.Point => 0,
                EventKind.Circle => e.CircleRadius,
                EventKind.Label => e.LabelText!.Length,
                EventKind.Rectangle => e.Rect.w * e.Rect.h,
                EventKind.ColoredLine => e.ColoredLineCoords.x1 + e.ColoredLineCoords.y1,
                EventKind.Transform => e.Transform.x + e.Transform.y + e.Transform.z + e.Transform.w,
                EventKind.RichText => e.RichTextText!.Length + e.RichTextSize + e.RichTextSpacing,
                EventKind.Error => e.ErrorMessage!.Length,
                _ => 0,
            };
        }
        return sum;
    }

    // ══════════════════════════════════════════
    // Match — Deconstruct / type patterns
    // ══════════════════════════════════════════

    [BenchmarkCategory("Match"), Benchmark(Description = "multiobj")]
    public double MatchMultiObj() {
        double sum = 0;
        var arr = _multiObj;
        for (int i = 0; i < arr.Length; i++) {
            var (kind, f0) = arr[i];
            sum += kind switch {
                EventKind.Point => 0,
                EventKind.Circle when f0 is double r => r,
                EventKind.Label when f0 is string s => s.Length,
                EventKind.Rectangle when f0 is float w => w,
                EventKind.ColoredLine when f0 is int x => x,
                EventKind.Transform when f0 is float x => x,
                EventKind.RichText when f0 is string t => t.Length,
                EventKind.Error when f0 is string m => m.Length,
                _ => 0,
            };
        }
        return sum;
    }

    [BenchmarkCategory("Match"), Benchmark(Description = "tupleobj")]
    public double MatchTupleObj() {
        double sum = 0;
        var arr = _tupleObj;
        for (int i = 0; i < arr.Length; i++) {
            var (kind, payload) = arr[i];
            sum += kind switch {
                EventKind.Point => 0,
                EventKind.Circle when payload is double r => r,
                EventKind.Label when payload is string s => s.Length,
                EventKind.Rectangle when payload is (float w, float h) => w * h,
                EventKind.ColoredLine when payload is (int x, int y, string _) => x + y,
                EventKind.Transform when payload is (float x, float y, float z, float w) => x + y + z + w,
                EventKind.RichText when payload is (string t, int sz, bool _, string _, double sp) => t.Length + sz + sp,
                EventKind.Error when payload is string m => m.Length,
                _ => 0,
            };
        }
        return sum;
    }

    [BenchmarkCategory("Match"), Benchmark(Description = "abstract class")]
    public double MatchClass() {
        double sum = 0;
        var arr = _class;
        for (int i = 0; i < arr.Length; i++) {
            sum += arr[i] switch {
                EventClass.PointClass => 0,
                EventClass.CircleClass c => c.Radius,
                EventClass.LabelClass l => l.Text.Length,
                EventClass.RectangleClass r => r.Width * r.Height,
                EventClass.ColoredLineClass cl => cl.X1 + cl.Y1,
                EventClass.TransformClass t => t.X + t.Y + t.Z + t.W,
                EventClass.RichTextClass rt => rt.Text.Length + rt.Size + rt.Spacing,
                EventClass.ErrorClass e => e.Message.Length,
                _ => 0,
            };
        }
        return sum;
    }

    [BenchmarkCategory("Match"), Benchmark(Description = "abstract record")]
    public double MatchRecord() {
        double sum = 0;
        var arr = _record;
        for (int i = 0; i < arr.Length; i++) {
            sum += arr[i] switch {
                EventRecord.PointRecord => 0,
                EventRecord.CircleRecord(var r) => r,
                EventRecord.LabelRecord(var t) => t.Length,
                EventRecord.RectangleRecord(var w, var h) => w * h,
                EventRecord.ColoredLineRecord(var x, var y, _) => x + y,
                EventRecord.TransformRecord(var x, var y, var z, var w) => x + y + z + w,
                EventRecord.RichTextRecord(var t, var sz, _, _, var sp) => t.Length + sz + sp,
                EventRecord.ErrorRecord(var m) => m.Length,
                _ => 0,
            };
        }
        return sum;
    }

    // ══════════════════════════════════════════
    // Copy throughput
    // ══════════════════════════════════════════

    [BenchmarkCategory("Copy"), Benchmark(Description = "multiobj")]
    public EventMultiObj[] CopyMultiObj() {
        var dst = new EventMultiObj[N];
        Array.Copy(_multiObj, dst, N);
        return dst;
    }

    [BenchmarkCategory("Copy"), Benchmark(Description = "tupleobj")]
    public EventTupleObj[] CopyTupleObj() {
        var dst = new EventTupleObj[N];
        Array.Copy(_tupleObj, dst, N);
        return dst;
    }

    [BenchmarkCategory("Copy"), Benchmark(Description = "hybrid explicit")]
    public EventHybrid[] CopyHybrid() {
        var dst = new EventHybrid[N];
        Array.Copy(_hybrid, dst, N);
        return dst;
    }

    [BenchmarkCategory("Copy"), Benchmark(Description = "abstract class")]
    public EventClass[] CopyClass() {
        var dst = new EventClass[N];
        Array.Copy(_class, dst, N);
        return dst;
    }

    [BenchmarkCategory("Copy"), Benchmark(Description = "abstract record")]
    public EventRecord[] CopyRecord() {
        var dst = new EventRecord[N];
        Array.Copy(_record, dst, N);
        return dst;
    }

    [BenchmarkCategory("Copy"), Benchmark(Description = "additive")]
    public EventAdditive[] CopyAdditive() {
        var dst = new EventAdditive[N];
        Array.Copy(_additive, dst, N);
        return dst;
    }

    [BenchmarkCategory("Copy"), Benchmark(Description = "unsafe inline")]
    public EventUnsafeInline[] CopyUnsafeInline() {
        var dst = new EventUnsafeInline[N];
        Array.Copy(_unsafeInline, dst, N);
        return dst;
    }

    [BenchmarkCategory("Copy"), Benchmark(Description = "unsafe long")]
    public EventUnsafeLong[] CopyUnsafeLong() {
        var dst = new EventUnsafeLong[N];
        Array.Copy(_unsafeLong, dst, N);
        return dst;
    }
}
