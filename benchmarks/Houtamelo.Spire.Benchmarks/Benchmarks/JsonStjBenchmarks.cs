using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Spire.Benchmarks.Helpers;

namespace Spire.Benchmarks;

/// System.Text.Json serialization round-trip for discriminated unions.
[Config(typeof(SpireConfig))]
public class JsonStjBenchmarks
{
    [Params(BenchN.Default)]
    public int N { get; set; }

    Types.EventAdditiveJson[] _additiveArr = null!;
    Types.EventRecordJson[] _recordArr = null!;
    string _additiveJson = null!;
    string _recordJson = null!;
    JsonSerializerOptions _options = null!;

    [GlobalSetup]
    public void Setup()
    {
        _options = new JsonSerializerOptions();
        _additiveArr = new Types.EventAdditiveJson[N];
        _recordArr = new Types.EventRecordJson[N];

        var rng = new Random(42);
        string[] pool = ["hello", "world", "red", "blue", "Arial", "Mono", "error!", "warn"];
        for (int i = 0; i < N; i++)
        {
            _additiveArr[i] = (i % 8) switch
            {
                0 => Types.EventAdditiveJson.Point(),
                1 => Types.EventAdditiveJson.Circle(rng.NextDouble()),
                2 => Types.EventAdditiveJson.Label(pool[rng.Next(pool.Length)]),
                3 => Types.EventAdditiveJson.Rectangle(rng.NextSingle(), rng.NextSingle()),
                4 => Types.EventAdditiveJson.ColoredLine(rng.Next(), rng.Next(), pool[rng.Next(pool.Length)]),
                5 => Types.EventAdditiveJson.Transform(rng.NextSingle(), rng.NextSingle(), rng.NextSingle(), rng.NextSingle()),
                6 => Types.EventAdditiveJson.RichText(pool[rng.Next(pool.Length)], rng.Next(8, 72), rng.Next(2) == 1, pool[rng.Next(pool.Length)], rng.NextDouble()),
                _ => Types.EventAdditiveJson.Error(pool[rng.Next(pool.Length)]),
            };
            _recordArr[i] = (i % 8) switch
            {
                0 => new Types.EventRecordJson.Point(),
                1 => new Types.EventRecordJson.Circle(rng.NextDouble()),
                2 => new Types.EventRecordJson.Label(pool[rng.Next(pool.Length)]),
                3 => new Types.EventRecordJson.Rectangle(rng.NextSingle(), rng.NextSingle()),
                4 => new Types.EventRecordJson.ColoredLine(rng.Next(), rng.Next(), pool[rng.Next(pool.Length)]),
                5 => new Types.EventRecordJson.Transform(rng.NextSingle(), rng.NextSingle(), rng.NextSingle(), rng.NextSingle()),
                6 => new Types.EventRecordJson.RichText(pool[rng.Next(pool.Length)], rng.Next(8, 72), rng.Next(2) == 1, pool[rng.Next(pool.Length)], rng.NextDouble()),
                _ => new Types.EventRecordJson.Error(pool[rng.Next(pool.Length)]),
            };
        }

        _additiveJson = JsonSerializer.Serialize(_additiveArr, _options);
        _recordJson = JsonSerializer.Serialize(_recordArr, _options);
    }

    [BenchmarkCategory("STJ Serialize"), Benchmark(Baseline = true, Description = "additive")]
    public string SerializeAdditive() => JsonSerializer.Serialize(_additiveArr, _options);

    [BenchmarkCategory("STJ Serialize"), Benchmark(Description = "record")]
    public string SerializeRecord() => JsonSerializer.Serialize(_recordArr, _options);

    [BenchmarkCategory("STJ Deserialize"), Benchmark(Baseline = true, Description = "additive")]
    public Types.EventAdditiveJson[]? DeserializeAdditive() => JsonSerializer.Deserialize<Types.EventAdditiveJson[]>(_additiveJson, _options);

    [BenchmarkCategory("STJ Deserialize"), Benchmark(Description = "record")]
    public Types.EventRecordJson[]? DeserializeRecord() => JsonSerializer.Deserialize<Types.EventRecordJson[]>(_recordJson, _options);

    [BenchmarkCategory("STJ RoundTrip"), Benchmark(Baseline = true, Description = "additive")]
    public Types.EventAdditiveJson[]? RoundTripAdditive()
    {
        var json = JsonSerializer.Serialize(_additiveArr, _options);
        return JsonSerializer.Deserialize<Types.EventAdditiveJson[]>(json, _options);
    }

    [BenchmarkCategory("STJ RoundTrip"), Benchmark(Description = "record")]
    public Types.EventRecordJson[]? RoundTripRecord()
    {
        var json = JsonSerializer.Serialize(_recordArr, _options);
        return JsonSerializer.Deserialize<Types.EventRecordJson[]>(json, _options);
    }
}
