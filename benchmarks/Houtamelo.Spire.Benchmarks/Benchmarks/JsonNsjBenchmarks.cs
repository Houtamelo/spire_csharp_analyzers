using BenchmarkDotNet.Attributes;
using Houtamelo.Spire.Benchmarks.Helpers;
using Newtonsoft.Json;

namespace Houtamelo.Spire.Benchmarks.Benchmarks;

/// Newtonsoft.Json serialization round-trip for discriminated unions.
[Config(typeof(SpireConfig))]
public class JsonNsjBenchmarks
{
    [Params(BenchN.Default)]
    public int N { get; set; }

    Types.EventAdditiveJson[] _additiveArr = null!;
    Types.EventRecordJson[] _recordArr = null!;
    string _additiveJson = null!;
    string _recordJson = null!;

    [GlobalSetup]
    public void Setup()
    {
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

        _additiveJson = JsonConvert.SerializeObject(_additiveArr);
        _recordJson = JsonConvert.SerializeObject(_recordArr);
    }

    [BenchmarkCategory("NSJ Serialize"), Benchmark(Baseline = true, Description = "additive")]
    public string SerializeAdditive() => JsonConvert.SerializeObject(_additiveArr);

    [BenchmarkCategory("NSJ Serialize"), Benchmark(Description = "record")]
    public string SerializeRecord() => JsonConvert.SerializeObject(_recordArr);

    [BenchmarkCategory("NSJ Deserialize"), Benchmark(Baseline = true, Description = "additive")]
    public Types.EventAdditiveJson[]? DeserializeAdditive() => JsonConvert.DeserializeObject<Types.EventAdditiveJson[]>(_additiveJson);

    [BenchmarkCategory("NSJ Deserialize"), Benchmark(Description = "record")]
    public Types.EventRecordJson[]? DeserializeRecord() => JsonConvert.DeserializeObject<Types.EventRecordJson[]>(_recordJson);

    [BenchmarkCategory("NSJ RoundTrip"), Benchmark(Baseline = true, Description = "additive")]
    public Types.EventAdditiveJson[]? RoundTripAdditive()
    {
        var json = JsonConvert.SerializeObject(_additiveArr);
        return JsonConvert.DeserializeObject<Types.EventAdditiveJson[]>(json);
    }

    [BenchmarkCategory("NSJ RoundTrip"), Benchmark(Description = "record")]
    public Types.EventRecordJson[]? RoundTripRecord()
    {
        var json = JsonConvert.SerializeObject(_recordArr);
        return JsonConvert.DeserializeObject<Types.EventRecordJson[]>(json);
    }
}
