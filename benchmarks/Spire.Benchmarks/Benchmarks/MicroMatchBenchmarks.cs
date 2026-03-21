using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace Spire.Benchmarks;

/// Single match operation per strategy. DisassemblyDiagnoser shows JIT codegen.
[Config(typeof(SpireDisasmConfig))]
public class MicroMatchBenchmarks
{
    EventAdditive _addTransform;
    EventBoxedFields _bfTransform;
    EventBoxedTuple _btTransform;
    EventOverlap _ovTransform;
    EventUnsafeOverlap _uoTransform;
    EventRecord _recTransform = null!;
    EventClass _clsTransform = null!;

    [GlobalSetup]
    public void Setup()
    {
        _addTransform = EventAdditive.Transform(1f, 2f, 3f, 4f);
        _bfTransform = EventBoxedFields.Transform(1f, 2f, 3f, 4f);
        _btTransform = EventBoxedTuple.Transform(1f, 2f, 3f, 4f);
        _ovTransform = EventOverlap.Transform(1f, 2f, 3f, 4f);
        _uoTransform = EventUnsafeOverlap.Transform(1f, 2f, 3f, 4f);
        _recTransform = new EventRecord.Transform(1f, 2f, 3f, 4f);
        _clsTransform = new EventClass.Transform(1f, 2f, 3f, 4f);
    }

    // ── Match Transform (4 float fields) ──

    [BenchmarkCategory("Micro Match"), Benchmark(Baseline = true, Description = "additive")]
    public double MatchAdditive()
    {
        _addTransform.Deconstruct(out _, out var x, out var y, out var z, out var w);
        return x * y + z * w;
    }

    [BenchmarkCategory("Micro Match"), Benchmark(Description = "boxedFields")]
    public double MatchBoxedFields()
    {
        _bfTransform.Deconstruct(out _, out var x, out var y, out var z, out var w);
        return x * y + z * w;
    }

    [BenchmarkCategory("Micro Match"), Benchmark(Description = "boxedTuple")]
    public double MatchBoxedTuple()
    {
        var t = ((float, float, float, float))_btTransform._payload!;
        return t.Item1 * t.Item2 + t.Item3 * t.Item4;
    }

    [BenchmarkCategory("Micro Match"), Benchmark(Description = "overlap")]
    public double MatchOverlap()
    {
        var e = _ovTransform;
        return e.x * e.y + e.z * e.w;
    }

    [BenchmarkCategory("Micro Match"), Benchmark(Description = "unsafeOverlap")]
    public double MatchUnsafeOverlap()
    {
        var e = _uoTransform;
        return e.x * e.y + e.z * e.w;
    }

    [BenchmarkCategory("Micro Match"), Benchmark(Description = "record")]
    public double MatchRecord()
    {
        var t = (EventRecord.Transform)_recTransform;
        return t.X * t.Y + t.Z * t.W;
    }

    [BenchmarkCategory("Micro Match"), Benchmark(Description = "class")]
    public double MatchClass()
    {
        var t = (EventClass.Transform)_clsTransform;
        return t.X * t.Y + t.Z * t.W;
    }
}
