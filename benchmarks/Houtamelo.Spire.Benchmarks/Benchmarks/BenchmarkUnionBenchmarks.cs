using BenchmarkDotNet.Attributes;
using Houtamelo.Spire.Benchmarks.Helpers;
using Houtamelo.Spire.Benchmarks.Types;

namespace Houtamelo.Spire.Benchmarks.Benchmarks;

[Config(typeof(SpireConfig))]
public class BenchmarkUnionBenchmarks
{
    [Params(BenchN.Default)]
    public int N { get; set; }

    private PhysBench[] _auto = null!;
    private PhysAuto[] _explicitAuto = null!;
    private PhysTyped[] _typed = null!;
    private PhysShared[] _shared = null!;
    private PhysRecord[] _record = null!;

    [GlobalSetup]
    public void Setup()
    {
        _auto = new PhysBench[N];
        _explicitAuto = new PhysAuto[N];
        _typed = new PhysTyped[N];
        _shared = new PhysShared[N];
        _record = new PhysRecord[N];
        for (var i = 0; i < N; i++)
        {
            _auto[i] = PhysBench.Position(i, i + 1);
            _explicitAuto[i] = PhysAuto.Position(i, i + 1);
            _typed[i] = PhysTyped.Position(i, i + 1);
            _shared[i] = PhysShared.Position(i, i + 1);
            _record[i] = new PhysRecord.Position(i, i + 1);
        }
    }

    [Benchmark(Baseline = true)]
    public double AutoRead() => Sum(_auto);

    [Benchmark]
    public double ExplicitAutoRead() => Sum(_explicitAuto);

    [Benchmark]
    public double TypedRead() => Sum(_typed);

    [Benchmark]
    public double SharedRead() => Sum(_shared);

    [Benchmark]
    public double RecordRead()
    {
        double sum = 0;
        foreach (var value in _record)
            if (value is PhysRecord.Position position)
                sum += position.x + position.y;
        return sum;
    }

    private static double Sum(PhysBench[] values)
    {
        double sum = 0;
        foreach (var value in values)
            sum += value.position.x + value.position.y;
        return sum;
    }

    private static double Sum(PhysAuto[] values)
    {
        double sum = 0;
        foreach (var value in values)
            sum += value.position.x + value.position.y;
        return sum;
    }

    private static double Sum(PhysTyped[] values)
    {
        double sum = 0;
        foreach (var value in values)
            sum += value.position.x + value.position.y;
        return sum;
    }

    private static double Sum(PhysShared[] values)
    {
        double sum = 0;
        foreach (var value in values)
            sum += value.position.x + value.position.y;
        return sum;
    }
}
