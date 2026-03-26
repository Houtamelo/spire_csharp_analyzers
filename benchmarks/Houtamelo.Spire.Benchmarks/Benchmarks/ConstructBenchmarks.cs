using BenchmarkDotNet.Attributes;
using Houtamelo.Spire.Benchmarks.Helpers;

namespace Houtamelo.Spire.Benchmarks.Benchmarks;

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
    public Types.EventAdditive[] EventAdditive_()
    {
        var arr = new Types.EventAdditive[N];
        ArrayFiller.Fill(arr, new Random(42), Distribution.Uniform);
        return arr;
    }

    [BenchmarkCategory("Event Construct"), Benchmark(Description = "boxedFields")]
    public Types.EventBoxedFields[] EventBoxedFields_()
    {
        var arr = new Types.EventBoxedFields[N];
        ArrayFiller.Fill(arr, new Random(42), Distribution.Uniform);
        return arr;
    }

    [BenchmarkCategory("Event Construct"), Benchmark(Description = "boxedTuple")]
    public Types.EventBoxedTuple[] EventBoxedTuple_()
    {
        var arr = new Types.EventBoxedTuple[N];
        ArrayFiller.Fill(arr, new Random(42), Distribution.Uniform);
        return arr;
    }

    [BenchmarkCategory("Event Construct"), Benchmark(Description = "overlap")]
    public Types.EventOverlap[] EventOverlap_()
    {
        var arr = new Types.EventOverlap[N];
        ArrayFiller.Fill(arr, new Random(42), Distribution.Uniform);
        return arr;
    }

    [BenchmarkCategory("Event Construct"), Benchmark(Description = "unsafeOverlap")]
    public Types.EventUnsafeOverlap[] EventUnsafeOverlap_()
    {
        var arr = new Types.EventUnsafeOverlap[N];
        ArrayFiller.Fill(arr, new Random(42), Distribution.Uniform);
        return arr;
    }

    [BenchmarkCategory("Event Construct"), Benchmark(Description = "record")]
    public Types.EventRecord[] EventRecord_()
    {
        var arr = new Types.EventRecord[N];
        ArrayFiller.Fill(arr, new Random(42), Distribution.Uniform);
        return arr;
    }

    [BenchmarkCategory("Event Construct"), Benchmark(Description = "class")]
    public Types.EventClass[] EventClass_()
    {
        var arr = new Types.EventClass[N];
        ArrayFiller.Fill(arr, new Random(42), Distribution.Uniform);
        return arr;
    }

    // ── Physics (unmanaged) ──

    [BenchmarkCategory("Physics Construct"), Benchmark(Baseline = true, Description = "additive")]
    public Types.PhysicsAdditive[] PhysicsAdditive_()
    {
        var arr = new Types.PhysicsAdditive[N];
        ArrayFiller.Fill(arr, new Random(42), Distribution.Uniform);
        return arr;
    }

    [BenchmarkCategory("Physics Construct"), Benchmark(Description = "boxedFields")]
    public Types.PhysicsBoxedFields[] PhysicsBoxedFields_()
    {
        var arr = new Types.PhysicsBoxedFields[N];
        ArrayFiller.Fill(arr, new Random(42), Distribution.Uniform);
        return arr;
    }

    [BenchmarkCategory("Physics Construct"), Benchmark(Description = "boxedTuple")]
    public Types.PhysicsBoxedTuple[] PhysicsBoxedTuple_()
    {
        var arr = new Types.PhysicsBoxedTuple[N];
        ArrayFiller.Fill(arr, new Random(42), Distribution.Uniform);
        return arr;
    }

    [BenchmarkCategory("Physics Construct"), Benchmark(Description = "overlap")]
    public Types.PhysicsOverlap[] PhysicsOverlap_()
    {
        var arr = new Types.PhysicsOverlap[N];
        ArrayFiller.Fill(arr, new Random(42), Distribution.Uniform);
        return arr;
    }

    [BenchmarkCategory("Physics Construct"), Benchmark(Description = "unsafeOverlap")]
    public Types.PhysicsUnsafeOverlap[] PhysicsUnsafeOverlap_()
    {
        var arr = new Types.PhysicsUnsafeOverlap[N];
        ArrayFiller.Fill(arr, new Random(42), Distribution.Uniform);
        return arr;
    }

    [BenchmarkCategory("Physics Construct"), Benchmark(Description = "record")]
    public Types.PhysicsRecord[] PhysicsRecord_()
    {
        var arr = new Types.PhysicsRecord[N];
        ArrayFiller.Fill(arr, new Random(42), Distribution.Uniform);
        return arr;
    }

    [BenchmarkCategory("Physics Construct"), Benchmark(Description = "class")]
    public Types.PhysicsClass[] PhysicsClass_()
    {
        var arr = new Types.PhysicsClass[N];
        ArrayFiller.Fill(arr, new Random(42), Distribution.Uniform);
        return arr;
    }
}
