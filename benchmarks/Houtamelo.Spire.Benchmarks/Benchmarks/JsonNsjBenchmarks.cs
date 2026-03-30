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
    Types.EventBoxedFieldsJson[] _boxedFieldsArr = null!;
    Types.EventBoxedTupleJson[] _boxedTupleArr = null!;
    Types.EventOverlapJson[] _overlapArr = null!;
    Types.EventUnsafeOverlapJson[] _unsafeOverlapArr = null!;
    Types.EventRecordJson[] _recordArr = null!;

    string _additiveJson = null!;
    string _boxedFieldsJson = null!;
    string _boxedTupleJson = null!;
    string _overlapJson = null!;
    string _unsafeOverlapJson = null!;
    string _recordJson = null!;

    [GlobalSetup]
    public void Setup()
    {
        _additiveArr = new Types.EventAdditiveJson[N];
        _boxedFieldsArr = new Types.EventBoxedFieldsJson[N];
        _boxedTupleArr = new Types.EventBoxedTupleJson[N];
        _overlapArr = new Types.EventOverlapJson[N];
        _unsafeOverlapArr = new Types.EventUnsafeOverlapJson[N];
        _recordArr = new Types.EventRecordJson[N];

        var rng = new Random(42);
        string[] pool = ["hello", "world", "red", "blue", "Arial", "Mono", "error!", "warn"];
        for (int i = 0; i < N; i++)
        {
            int v = i % 8;
            _additiveArr[i] = MakeAdditive(v, rng, pool);
            _boxedFieldsArr[i] = MakeBoxedFields(v, rng, pool);
            _boxedTupleArr[i] = MakeBoxedTuple(v, rng, pool);
            _overlapArr[i] = MakeOverlap(v, rng, pool);
            _unsafeOverlapArr[i] = MakeUnsafeOverlap(v, rng, pool);
            _recordArr[i] = MakeRecord(v, rng, pool);
        }

        _additiveJson = JsonConvert.SerializeObject(_additiveArr);
        _boxedFieldsJson = JsonConvert.SerializeObject(_boxedFieldsArr);
        _boxedTupleJson = JsonConvert.SerializeObject(_boxedTupleArr);
        _overlapJson = JsonConvert.SerializeObject(_overlapArr);
        _unsafeOverlapJson = JsonConvert.SerializeObject(_unsafeOverlapArr);
        _recordJson = JsonConvert.SerializeObject(_recordArr);
    }

    // ── Serialize ──

    [BenchmarkCategory("NSJ Serialize"), Benchmark(Baseline = true, Description = "additive")]
    public string SerializeAdditive() => JsonConvert.SerializeObject(_additiveArr);

    [BenchmarkCategory("NSJ Serialize"), Benchmark(Description = "boxedFields")]
    public string SerializeBoxedFields() => JsonConvert.SerializeObject(_boxedFieldsArr);

    [BenchmarkCategory("NSJ Serialize"), Benchmark(Description = "boxedTuple")]
    public string SerializeBoxedTuple() => JsonConvert.SerializeObject(_boxedTupleArr);

    [BenchmarkCategory("NSJ Serialize"), Benchmark(Description = "overlap")]
    public string SerializeOverlap() => JsonConvert.SerializeObject(_overlapArr);

    [BenchmarkCategory("NSJ Serialize"), Benchmark(Description = "unsafeOverlap")]
    public string SerializeUnsafeOverlap() => JsonConvert.SerializeObject(_unsafeOverlapArr);

    [BenchmarkCategory("NSJ Serialize"), Benchmark(Description = "record")]
    public string SerializeRecord() => JsonConvert.SerializeObject(_recordArr);

    // ── Deserialize ──

    [BenchmarkCategory("NSJ Deserialize"), Benchmark(Baseline = true, Description = "additive")]
    public Types.EventAdditiveJson[]? DeserializeAdditive() => JsonConvert.DeserializeObject<Types.EventAdditiveJson[]>(_additiveJson);

    [BenchmarkCategory("NSJ Deserialize"), Benchmark(Description = "boxedFields")]
    public Types.EventBoxedFieldsJson[]? DeserializeBoxedFields() => JsonConvert.DeserializeObject<Types.EventBoxedFieldsJson[]>(_boxedFieldsJson);

    [BenchmarkCategory("NSJ Deserialize"), Benchmark(Description = "boxedTuple")]
    public Types.EventBoxedTupleJson[]? DeserializeBoxedTuple() => JsonConvert.DeserializeObject<Types.EventBoxedTupleJson[]>(_boxedTupleJson);

    [BenchmarkCategory("NSJ Deserialize"), Benchmark(Description = "overlap")]
    public Types.EventOverlapJson[]? DeserializeOverlap() => JsonConvert.DeserializeObject<Types.EventOverlapJson[]>(_overlapJson);

    [BenchmarkCategory("NSJ Deserialize"), Benchmark(Description = "unsafeOverlap")]
    public Types.EventUnsafeOverlapJson[]? DeserializeUnsafeOverlap() => JsonConvert.DeserializeObject<Types.EventUnsafeOverlapJson[]>(_unsafeOverlapJson);

    [BenchmarkCategory("NSJ Deserialize"), Benchmark(Description = "record")]
    public Types.EventRecordJson[]? DeserializeRecord() => JsonConvert.DeserializeObject<Types.EventRecordJson[]>(_recordJson);

    // ── RoundTrip ──

    [BenchmarkCategory("NSJ RoundTrip"), Benchmark(Baseline = true, Description = "additive")]
    public Types.EventAdditiveJson[]? RoundTripAdditive()
    {
        var json = JsonConvert.SerializeObject(_additiveArr);
        return JsonConvert.DeserializeObject<Types.EventAdditiveJson[]>(json);
    }

    [BenchmarkCategory("NSJ RoundTrip"), Benchmark(Description = "boxedFields")]
    public Types.EventBoxedFieldsJson[]? RoundTripBoxedFields()
    {
        var json = JsonConvert.SerializeObject(_boxedFieldsArr);
        return JsonConvert.DeserializeObject<Types.EventBoxedFieldsJson[]>(json);
    }

    [BenchmarkCategory("NSJ RoundTrip"), Benchmark(Description = "boxedTuple")]
    public Types.EventBoxedTupleJson[]? RoundTripBoxedTuple()
    {
        var json = JsonConvert.SerializeObject(_boxedTupleArr);
        return JsonConvert.DeserializeObject<Types.EventBoxedTupleJson[]>(json);
    }

    [BenchmarkCategory("NSJ RoundTrip"), Benchmark(Description = "overlap")]
    public Types.EventOverlapJson[]? RoundTripOverlap()
    {
        var json = JsonConvert.SerializeObject(_overlapArr);
        return JsonConvert.DeserializeObject<Types.EventOverlapJson[]>(json);
    }

    [BenchmarkCategory("NSJ RoundTrip"), Benchmark(Description = "unsafeOverlap")]
    public Types.EventUnsafeOverlapJson[]? RoundTripUnsafeOverlap()
    {
        var json = JsonConvert.SerializeObject(_unsafeOverlapArr);
        return JsonConvert.DeserializeObject<Types.EventUnsafeOverlapJson[]>(json);
    }

    [BenchmarkCategory("NSJ RoundTrip"), Benchmark(Description = "record")]
    public Types.EventRecordJson[]? RoundTripRecord()
    {
        var json = JsonConvert.SerializeObject(_recordArr);
        return JsonConvert.DeserializeObject<Types.EventRecordJson[]>(json);
    }

    // ── Factories ──

    static Types.EventAdditiveJson MakeAdditive(int v, Random rng, string[] pool) => v switch
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

    static Types.EventBoxedFieldsJson MakeBoxedFields(int v, Random rng, string[] pool) => v switch
    {
        0 => Types.EventBoxedFieldsJson.Point(),
        1 => Types.EventBoxedFieldsJson.Circle(rng.NextDouble()),
        2 => Types.EventBoxedFieldsJson.Label(pool[rng.Next(pool.Length)]),
        3 => Types.EventBoxedFieldsJson.Rectangle(rng.NextSingle(), rng.NextSingle()),
        4 => Types.EventBoxedFieldsJson.ColoredLine(rng.Next(), rng.Next(), pool[rng.Next(pool.Length)]),
        5 => Types.EventBoxedFieldsJson.Transform(rng.NextSingle(), rng.NextSingle(), rng.NextSingle(), rng.NextSingle()),
        6 => Types.EventBoxedFieldsJson.RichText(pool[rng.Next(pool.Length)], rng.Next(8, 72), rng.Next(2) == 1, pool[rng.Next(pool.Length)], rng.NextDouble()),
        _ => Types.EventBoxedFieldsJson.Error(pool[rng.Next(pool.Length)]),
    };

    static Types.EventBoxedTupleJson MakeBoxedTuple(int v, Random rng, string[] pool) => v switch
    {
        0 => Types.EventBoxedTupleJson.Point(),
        1 => Types.EventBoxedTupleJson.Circle(rng.NextDouble()),
        2 => Types.EventBoxedTupleJson.Label(pool[rng.Next(pool.Length)]),
        3 => Types.EventBoxedTupleJson.Rectangle(rng.NextSingle(), rng.NextSingle()),
        4 => Types.EventBoxedTupleJson.ColoredLine(rng.Next(), rng.Next(), pool[rng.Next(pool.Length)]),
        5 => Types.EventBoxedTupleJson.Transform(rng.NextSingle(), rng.NextSingle(), rng.NextSingle(), rng.NextSingle()),
        6 => Types.EventBoxedTupleJson.RichText(pool[rng.Next(pool.Length)], rng.Next(8, 72), rng.Next(2) == 1, pool[rng.Next(pool.Length)], rng.NextDouble()),
        _ => Types.EventBoxedTupleJson.Error(pool[rng.Next(pool.Length)]),
    };

    static Types.EventOverlapJson MakeOverlap(int v, Random rng, string[] pool) => v switch
    {
        0 => Types.EventOverlapJson.Point(),
        1 => Types.EventOverlapJson.Circle(rng.NextDouble()),
        2 => Types.EventOverlapJson.Label(pool[rng.Next(pool.Length)]),
        3 => Types.EventOverlapJson.Rectangle(rng.NextSingle(), rng.NextSingle()),
        4 => Types.EventOverlapJson.ColoredLine(rng.Next(), rng.Next(), pool[rng.Next(pool.Length)]),
        5 => Types.EventOverlapJson.Transform(rng.NextSingle(), rng.NextSingle(), rng.NextSingle(), rng.NextSingle()),
        6 => Types.EventOverlapJson.RichText(pool[rng.Next(pool.Length)], rng.Next(8, 72), rng.Next(2) == 1, pool[rng.Next(pool.Length)], rng.NextDouble()),
        _ => Types.EventOverlapJson.Error(pool[rng.Next(pool.Length)]),
    };

    static Types.EventUnsafeOverlapJson MakeUnsafeOverlap(int v, Random rng, string[] pool) => v switch
    {
        0 => Types.EventUnsafeOverlapJson.Point(),
        1 => Types.EventUnsafeOverlapJson.Circle(rng.NextDouble()),
        2 => Types.EventUnsafeOverlapJson.Label(pool[rng.Next(pool.Length)]),
        3 => Types.EventUnsafeOverlapJson.Rectangle(rng.NextSingle(), rng.NextSingle()),
        4 => Types.EventUnsafeOverlapJson.ColoredLine(rng.Next(), rng.Next(), pool[rng.Next(pool.Length)]),
        5 => Types.EventUnsafeOverlapJson.Transform(rng.NextSingle(), rng.NextSingle(), rng.NextSingle(), rng.NextSingle()),
        6 => Types.EventUnsafeOverlapJson.RichText(pool[rng.Next(pool.Length)], rng.Next(8, 72), rng.Next(2) == 1, pool[rng.Next(pool.Length)], rng.NextDouble()),
        _ => Types.EventUnsafeOverlapJson.Error(pool[rng.Next(pool.Length)]),
    };

    static Types.EventRecordJson MakeRecord(int v, Random rng, string[] pool) => v switch
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
