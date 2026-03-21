using System.Text.Json;
using BenchmarkDotNet.Attributes;

namespace Spire.Benchmarks;

/// System.Text.Json serialization round-trip for discriminated unions.
[Config(typeof(SpireConfig))]
public class JsonStjBenchmarks
{
    [Params(BenchN.Default)]
    public int N { get; set; }

    EventAdditiveJson[] _additiveArr = null!;
    EventRecordJson[] _recordArr = null!;
    string _additiveJson = null!;
    string _recordJson = null!;
    JsonSerializerOptions _options = null!;

    [GlobalSetup]
    public void Setup()
    {
        _options = new JsonSerializerOptions();
        _additiveArr = new EventAdditiveJson[N];
        _recordArr = new EventRecordJson[N];

        var rng = new Random(42);
        string[] pool = ["hello", "world", "red", "blue", "Arial", "Mono", "error!", "warn"];
        for (int i = 0; i < N; i++)
        {
            _additiveArr[i] = (i % 8) switch
            {
                0 => EventAdditiveJson.Point(),
                1 => EventAdditiveJson.Circle(rng.NextDouble()),
                2 => EventAdditiveJson.Label(pool[rng.Next(pool.Length)]),
                3 => EventAdditiveJson.Rectangle(rng.NextSingle(), rng.NextSingle()),
                4 => EventAdditiveJson.ColoredLine(rng.Next(), rng.Next(), pool[rng.Next(pool.Length)]),
                5 => EventAdditiveJson.Transform(rng.NextSingle(), rng.NextSingle(), rng.NextSingle(), rng.NextSingle()),
                6 => EventAdditiveJson.RichText(pool[rng.Next(pool.Length)], rng.Next(8, 72), rng.Next(2) == 1, pool[rng.Next(pool.Length)], rng.NextDouble()),
                _ => EventAdditiveJson.Error(pool[rng.Next(pool.Length)]),
            };
            _recordArr[i] = (i % 8) switch
            {
                0 => new EventRecordJson.Point(),
                1 => new EventRecordJson.Circle(rng.NextDouble()),
                2 => new EventRecordJson.Label(pool[rng.Next(pool.Length)]),
                3 => new EventRecordJson.Rectangle(rng.NextSingle(), rng.NextSingle()),
                4 => new EventRecordJson.ColoredLine(rng.Next(), rng.Next(), pool[rng.Next(pool.Length)]),
                5 => new EventRecordJson.Transform(rng.NextSingle(), rng.NextSingle(), rng.NextSingle(), rng.NextSingle()),
                6 => new EventRecordJson.RichText(pool[rng.Next(pool.Length)], rng.Next(8, 72), rng.Next(2) == 1, pool[rng.Next(pool.Length)], rng.NextDouble()),
                _ => new EventRecordJson.Error(pool[rng.Next(pool.Length)]),
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
    public EventAdditiveJson[]? DeserializeAdditive() => JsonSerializer.Deserialize<EventAdditiveJson[]>(_additiveJson, _options);

    [BenchmarkCategory("STJ Deserialize"), Benchmark(Description = "record")]
    public EventRecordJson[]? DeserializeRecord() => JsonSerializer.Deserialize<EventRecordJson[]>(_recordJson, _options);

    [BenchmarkCategory("STJ RoundTrip"), Benchmark(Baseline = true, Description = "additive")]
    public EventAdditiveJson[]? RoundTripAdditive()
    {
        var json = JsonSerializer.Serialize(_additiveArr, _options);
        return JsonSerializer.Deserialize<EventAdditiveJson[]>(json, _options);
    }

    [BenchmarkCategory("STJ RoundTrip"), Benchmark(Description = "record")]
    public EventRecordJson[]? RoundTripRecord()
    {
        var json = JsonSerializer.Serialize(_recordArr, _options);
        return JsonSerializer.Deserialize<EventRecordJson[]>(json, _options);
    }
}
