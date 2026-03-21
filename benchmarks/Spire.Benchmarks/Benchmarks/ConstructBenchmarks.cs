using BenchmarkDotNet.Attributes;

namespace Spire.Benchmarks;

[Config(typeof(SpireConfig))]
public class ConstructBenchmarks
{
    [Params(BenchN.Default)]
    public int N { get; set; }

    Random _rng = null!;

    [GlobalSetup]
    public void Setup() => _rng = new Random(42);

    // ── Event (mixed) ──

    [BenchmarkCategory("Event Construct"), Benchmark(Baseline = true, Description = "additive")]
    public EventAdditive[] EventAdditive_()
    {
        var arr = new EventAdditive[N];
        ArrayFiller.Fill(arr, new Random(42), Distribution.Uniform);
        return arr;
    }

    [BenchmarkCategory("Event Construct"), Benchmark(Description = "boxedFields")]
    public EventBoxedFields[] EventBoxedFields_()
    {
        var arr = new EventBoxedFields[N];
        ArrayFiller.Fill(arr, new Random(42), Distribution.Uniform);
        return arr;
    }

    [BenchmarkCategory("Event Construct"), Benchmark(Description = "boxedTuple")]
    public EventBoxedTuple[] EventBoxedTuple_()
    {
        var arr = new EventBoxedTuple[N];
        ArrayFiller.Fill(arr, new Random(42), Distribution.Uniform);
        return arr;
    }

    [BenchmarkCategory("Event Construct"), Benchmark(Description = "overlap")]
    public EventOverlap[] EventOverlap_()
    {
        var arr = new EventOverlap[N];
        ArrayFiller.Fill(arr, new Random(42), Distribution.Uniform);
        return arr;
    }

    [BenchmarkCategory("Event Construct"), Benchmark(Description = "unsafeOverlap")]
    public EventUnsafeOverlap[] EventUnsafeOverlap_()
    {
        var arr = new EventUnsafeOverlap[N];
        ArrayFiller.Fill(arr, new Random(42), Distribution.Uniform);
        return arr;
    }

    [BenchmarkCategory("Event Construct"), Benchmark(Description = "record")]
    public EventRecord[] EventRecord_()
    {
        var arr = new EventRecord[N];
        ArrayFiller.Fill(arr, new Random(42), Distribution.Uniform);
        return arr;
    }

    [BenchmarkCategory("Event Construct"), Benchmark(Description = "class")]
    public EventClass[] EventClass_()
    {
        var arr = new EventClass[N];
        ArrayFiller.Fill(arr, new Random(42), Distribution.Uniform);
        return arr;
    }

    // ── Physics (unmanaged) ──

    [BenchmarkCategory("Physics Construct"), Benchmark(Baseline = true, Description = "additive")]
    public PhysicsAdditive[] PhysicsAdditive_()
    {
        var arr = new PhysicsAdditive[N];
        ArrayFiller.Fill(arr, new Random(42), Distribution.Uniform);
        return arr;
    }

    [BenchmarkCategory("Physics Construct"), Benchmark(Description = "boxedFields")]
    public PhysicsBoxedFields[] PhysicsBoxedFields_()
    {
        var arr = new PhysicsBoxedFields[N];
        ArrayFiller.Fill(arr, new Random(42), Distribution.Uniform);
        return arr;
    }

    [BenchmarkCategory("Physics Construct"), Benchmark(Description = "boxedTuple")]
    public PhysicsBoxedTuple[] PhysicsBoxedTuple_()
    {
        var arr = new PhysicsBoxedTuple[N];
        ArrayFiller.Fill(arr, new Random(42), Distribution.Uniform);
        return arr;
    }

    [BenchmarkCategory("Physics Construct"), Benchmark(Description = "overlap")]
    public PhysicsOverlap[] PhysicsOverlap_()
    {
        var arr = new PhysicsOverlap[N];
        ArrayFiller.Fill(arr, new Random(42), Distribution.Uniform);
        return arr;
    }

    [BenchmarkCategory("Physics Construct"), Benchmark(Description = "unsafeOverlap")]
    public PhysicsUnsafeOverlap[] PhysicsUnsafeOverlap_()
    {
        var arr = new PhysicsUnsafeOverlap[N];
        ArrayFiller.Fill(arr, new Random(42), Distribution.Uniform);
        return arr;
    }

    [BenchmarkCategory("Physics Construct"), Benchmark(Description = "record")]
    public PhysicsRecord[] PhysicsRecord_()
    {
        var arr = new PhysicsRecord[N];
        ArrayFiller.Fill(arr, new Random(42), Distribution.Uniform);
        return arr;
    }

    [BenchmarkCategory("Physics Construct"), Benchmark(Description = "class")]
    public PhysicsClass[] PhysicsClass_()
    {
        var arr = new PhysicsClass[N];
        ArrayFiller.Fill(arr, new Random(42), Distribution.Uniform);
        return arr;
    }
}
