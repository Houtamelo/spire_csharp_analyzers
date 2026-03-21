using Newtonsoft.Json;
using BenchmarkDotNet.Attributes;

namespace Spire.Benchmarks;

/// Newtonsoft.Json serialization round-trip for discriminated unions.
[Config(typeof(SpireConfig))]
public class JsonNsjBenchmarks
{
    [Params(BenchN.Default)]
    public int N { get; set; }

    EventAdditiveJson[] _additiveArr = null!;
    EventRecordJson[] _recordArr = null!;
    string _additiveJson = null!;
    string _recordJson = null!;

    [GlobalSetup]
    public void Setup()
    {
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

        _additiveJson = JsonConvert.SerializeObject(_additiveArr);
        _recordJson = JsonConvert.SerializeObject(_recordArr);
    }

    [BenchmarkCategory("NSJ Serialize"), Benchmark(Baseline = true, Description = "additive")]
    public string SerializeAdditive() => JsonConvert.SerializeObject(_additiveArr);

    [BenchmarkCategory("NSJ Serialize"), Benchmark(Description = "record")]
    public string SerializeRecord() => JsonConvert.SerializeObject(_recordArr);

    [BenchmarkCategory("NSJ Deserialize"), Benchmark(Baseline = true, Description = "additive")]
    public EventAdditiveJson[]? DeserializeAdditive() => JsonConvert.DeserializeObject<EventAdditiveJson[]>(_additiveJson);

    [BenchmarkCategory("NSJ Deserialize"), Benchmark(Description = "record")]
    public EventRecordJson[]? DeserializeRecord() => JsonConvert.DeserializeObject<EventRecordJson[]>(_recordJson);

    [BenchmarkCategory("NSJ RoundTrip"), Benchmark(Baseline = true, Description = "additive")]
    public EventAdditiveJson[]? RoundTripAdditive()
    {
        var json = JsonConvert.SerializeObject(_additiveArr);
        return JsonConvert.DeserializeObject<EventAdditiveJson[]>(json);
    }

    [BenchmarkCategory("NSJ RoundTrip"), Benchmark(Description = "record")]
    public EventRecordJson[]? RoundTripRecord()
    {
        var json = JsonConvert.SerializeObject(_recordArr);
        return JsonConvert.DeserializeObject<EventRecordJson[]>(json);
    }
}
