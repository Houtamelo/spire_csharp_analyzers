using BenchmarkDotNet.Attributes;
using Houtamelo.Spire.Benchmarks.Helpers;
using Houtamelo.Spire.Benchmarks.Types;

namespace Houtamelo.Spire.Benchmarks.Benchmarks;

[Config(typeof(SpireConfig))]
public class VariantPayloadBenchmarks
{
    [Params(BenchN.Default)]
    public int N { get; set; }

    private VariantPayloadBenchmarkUnion[] _values = null!;

    [GlobalSetup]
    public void Setup()
    {
        _values = new VariantPayloadBenchmarkUnion[N];
        for (var i = 0; i < _values.Length; i++)
        {
            _values[i] = (i % 3) switch
            {
                0 => VariantPayloadBenchmarkUnion.Empty(),
                1 => VariantPayloadBenchmarkUnion.Transform(i, i + 1, i + 2, i + 3),
                _ => VariantPayloadBenchmarkUnion.Label(i.ToString()),
            };
        }
    }

    [Benchmark]
    public VariantPayloadBenchmarkUnion[] Construct()
    {
        var result = new VariantPayloadBenchmarkUnion[N];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = (i % 3) switch
            {
                0 => VariantPayloadBenchmarkUnion.Empty(),
                1 => VariantPayloadBenchmarkUnion.Transform(i, i + 1, i + 2, i + 3),
                _ => VariantPayloadBenchmarkUnion.Label(i.ToString()),
            };
        }

        return result;
    }

    [Benchmark]
    public double MatchAndRead()
    {
        double sum = 0;
        foreach (var value in _values)
        {
            sum += value.kind switch
            {
                VariantPayloadBenchmarkUnion.Kind.Transform => value.transform.x + value.transform.y,
                VariantPayloadBenchmarkUnion.Kind.Label => value.label.text.Length,
                _ => 0,
            };
        }

        return sum;
    }
}
