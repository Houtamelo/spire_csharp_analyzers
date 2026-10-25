using BenchmarkDotNet.Attributes;
using Houtamelo.Spire.Benchmarks.Helpers;

namespace Houtamelo.Spire.Benchmarks.Benchmarks;

[Config(typeof(SpireConfig))]
public class ConstructBenchmarks
{
    [Params(BenchN.Default)]
    public int N { get; set; }

    [BenchmarkCategory("Event Construct"), Benchmark(Baseline = true)]
    public Types.EventBench[] EventAuto()
    {
        var values = new Types.EventBench[N];
        ArrayFiller.Fill(values, new Random(42), Distribution.Uniform);
        return values;
    }

    [BenchmarkCategory("Event Construct"), Benchmark]
    public Types.EventAuto[] EventExplicitAuto()
    {
        var values = new Types.EventAuto[N];
        ArrayFiller.Fill(values, new Random(42), Distribution.Uniform);
        return values;
    }

    [BenchmarkCategory("Event Construct"), Benchmark]
    public Types.EventTyped[] EventTyped()
    {
        var values = new Types.EventTyped[N];
        ArrayFiller.Fill(values, new Random(42), Distribution.Uniform);
        return values;
    }

    [BenchmarkCategory("Event Construct"), Benchmark]
    public Types.EventShared[] EventShared()
    {
        var values = new Types.EventShared[N];
        ArrayFiller.Fill(values, new Random(42), Distribution.Uniform);
        return values;
    }

    [BenchmarkCategory("Event Construct"), Benchmark]
    public Types.EventRecord[] EventRecordBaseline()
    {
        var values = new Types.EventRecord[N];
        ArrayFiller.Fill(values, new Random(42), Distribution.Uniform);
        return values;
    }

    [BenchmarkCategory("Event Construct"), Benchmark]
    public Types.EventNative[] EventNative()
    {
        var values = new Types.EventNative[N];
        ArrayFiller.Fill(values, new Random(42), Distribution.Uniform);
        return values;
    }

    [BenchmarkCategory("Physics Construct"), Benchmark(Baseline = true)]
    public Types.PhysicsBench[] PhysicsAuto()
    {
        var values = new Types.PhysicsBench[N];
        ArrayFiller.Fill(values, new Random(42), Distribution.Uniform);
        return values;
    }

    [BenchmarkCategory("Physics Construct"), Benchmark]
    public Types.PhysicsAuto[] PhysicsExplicitAuto()
    {
        var values = new Types.PhysicsAuto[N];
        ArrayFiller.Fill(values, new Random(42), Distribution.Uniform);
        return values;
    }

    [BenchmarkCategory("Physics Construct"), Benchmark]
    public Types.PhysicsTyped[] PhysicsTyped()
    {
        var values = new Types.PhysicsTyped[N];
        ArrayFiller.Fill(values, new Random(42), Distribution.Uniform);
        return values;
    }

    [BenchmarkCategory("Physics Construct"), Benchmark]
    public Types.PhysicsShared[] PhysicsShared()
    {
        var values = new Types.PhysicsShared[N];
        ArrayFiller.Fill(values, new Random(42), Distribution.Uniform);
        return values;
    }

    [BenchmarkCategory("Physics Construct"), Benchmark]
    public Types.PhysicsRecord[] PhysicsRecordBaseline()
    {
        var values = new Types.PhysicsRecord[N];
        ArrayFiller.Fill(values, new Random(42), Distribution.Uniform);
        return values;
    }

    [BenchmarkCategory("Physics Construct"), Benchmark]
    public Types.PhysicsNative[] PhysicsNative()
    {
        var values = new Types.PhysicsNative[N];
        ArrayFiller.Fill(values, new Random(42), Distribution.Uniform);
        return values;
    }
}
