using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Houtamelo.Spire.Benchmarks.Helpers;

namespace Houtamelo.Spire.Benchmarks.Benchmarks;

/// System.Text.Json serialization round-trip for discriminated unions.
[Config(typeof(SpireConfig))]
public class JsonStjBenchmarks
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

    JsonSerializerOptions _options = null!;

    [GlobalSetup]
    public void Setup()
    {
        _options = new JsonSerializerOptions();
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

        _additiveJson = JsonSerializer.Serialize(_additiveArr, _options);
        _boxedFieldsJson = JsonSerializer.Serialize(_boxedFieldsArr, _options);
        _boxedTupleJson = JsonSerializer.Serialize(_boxedTupleArr, _options);
        _overlapJson = JsonSerializer.Serialize(_overlapArr, _options);
        _unsafeOverlapJson = JsonSerializer.Serialize(_unsafeOverlapArr, _options);
        _recordJson = JsonSerializer.Serialize(_recordArr, _options);
    }

    // ── Serialize ──

    [BenchmarkCategory("STJ Serialize"), Benchmark(Baseline = true, Description = "additive")]
    public string SerializeAdditive() => JsonSerializer.Serialize(_additiveArr, _options);

    [BenchmarkCategory("STJ Serialize"), Benchmark(Description = "boxedFields")]
    public string SerializeBoxedFields() => JsonSerializer.Serialize(_boxedFieldsArr, _options);

    [BenchmarkCategory("STJ Serialize"), Benchmark(Description = "boxedTuple")]
    public string SerializeBoxedTuple() => JsonSerializer.Serialize(_boxedTupleArr, _options);

    [BenchmarkCategory("STJ Serialize"), Benchmark(Description = "overlap")]
    public string SerializeOverlap() => JsonSerializer.Serialize(_overlapArr, _options);

    [BenchmarkCategory("STJ Serialize"), Benchmark(Description = "unsafeOverlap")]
    public string SerializeUnsafeOverlap() => JsonSerializer.Serialize(_unsafeOverlapArr, _options);

    [BenchmarkCategory("STJ Serialize"), Benchmark(Description = "record")]
    public string SerializeRecord() => JsonSerializer.Serialize(_recordArr, _options);

    // ── Deserialize ──

    [BenchmarkCategory("STJ Deserialize"), Benchmark(Baseline = true, Description = "additive")]
    public Types.EventAdditiveJson[]? DeserializeAdditive() => JsonSerializer.Deserialize<Types.EventAdditiveJson[]>(_additiveJson, _options);

    [BenchmarkCategory("STJ Deserialize"), Benchmark(Description = "boxedFields")]
    public Types.EventBoxedFieldsJson[]? DeserializeBoxedFields() => JsonSerializer.Deserialize<Types.EventBoxedFieldsJson[]>(_boxedFieldsJson, _options);

    [BenchmarkCategory("STJ Deserialize"), Benchmark(Description = "boxedTuple")]
    public Types.EventBoxedTupleJson[]? DeserializeBoxedTuple() => JsonSerializer.Deserialize<Types.EventBoxedTupleJson[]>(_boxedTupleJson, _options);

    [BenchmarkCategory("STJ Deserialize"), Benchmark(Description = "overlap")]
    public Types.EventOverlapJson[]? DeserializeOverlap() => JsonSerializer.Deserialize<Types.EventOverlapJson[]>(_overlapJson, _options);

    [BenchmarkCategory("STJ Deserialize"), Benchmark(Description = "unsafeOverlap")]
    public Types.EventUnsafeOverlapJson[]? DeserializeUnsafeOverlap() => JsonSerializer.Deserialize<Types.EventUnsafeOverlapJson[]>(_unsafeOverlapJson, _options);

    [BenchmarkCategory("STJ Deserialize"), Benchmark(Description = "record")]
    public Types.EventRecordJson[]? DeserializeRecord() => JsonSerializer.Deserialize<Types.EventRecordJson[]>(_recordJson, _options);

    // ── RoundTrip ──

    [BenchmarkCategory("STJ RoundTrip"), Benchmark(Baseline = true, Description = "additive")]
    public Types.EventAdditiveJson[]? RoundTripAdditive()
    {
        var json = JsonSerializer.Serialize(_additiveArr, _options);
        return JsonSerializer.Deserialize<Types.EventAdditiveJson[]>(json, _options);
    }

    [BenchmarkCategory("STJ RoundTrip"), Benchmark(Description = "boxedFields")]
    public Types.EventBoxedFieldsJson[]? RoundTripBoxedFields()
    {
        var json = JsonSerializer.Serialize(_boxedFieldsArr, _options);
        return JsonSerializer.Deserialize<Types.EventBoxedFieldsJson[]>(json, _options);
    }

    [BenchmarkCategory("STJ RoundTrip"), Benchmark(Description = "boxedTuple")]
    public Types.EventBoxedTupleJson[]? RoundTripBoxedTuple()
    {
        var json = JsonSerializer.Serialize(_boxedTupleArr, _options);
        return JsonSerializer.Deserialize<Types.EventBoxedTupleJson[]>(json, _options);
    }

    [BenchmarkCategory("STJ RoundTrip"), Benchmark(Description = "overlap")]
    public Types.EventOverlapJson[]? RoundTripOverlap()
    {
        var json = JsonSerializer.Serialize(_overlapArr, _options);
        return JsonSerializer.Deserialize<Types.EventOverlapJson[]>(json, _options);
    }

    [BenchmarkCategory("STJ RoundTrip"), Benchmark(Description = "unsafeOverlap")]
    public Types.EventUnsafeOverlapJson[]? RoundTripUnsafeOverlap()
    {
        var json = JsonSerializer.Serialize(_unsafeOverlapArr, _options);
        return JsonSerializer.Deserialize<Types.EventUnsafeOverlapJson[]>(json, _options);
    }

    [BenchmarkCategory("STJ RoundTrip"), Benchmark(Description = "record")]
    public Types.EventRecordJson[]? RoundTripRecord()
    {
        var json = JsonSerializer.Serialize(_recordArr, _options);
        return JsonSerializer.Deserialize<Types.EventRecordJson[]>(json, _options);
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
