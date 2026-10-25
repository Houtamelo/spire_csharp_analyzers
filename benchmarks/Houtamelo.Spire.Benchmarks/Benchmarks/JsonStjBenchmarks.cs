using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Houtamelo.Spire.Benchmarks.Helpers;

namespace Houtamelo.Spire.Benchmarks.Benchmarks;

[Config(typeof(SpireConfig))]
public class JsonStjBenchmarks
{
    [Params(BenchN.Default)]
    public int N { get; set; }

    private Types.EventJsonBench[] _auto = null!;
    private Types.EventJsonAuto[] _explicitAuto = null!;
    private Types.EventJsonTyped[] _typed = null!;
    private Types.EventJsonShared[] _shared = null!;
    private Types.EventJsonRecord[] _record = null!;
    private string _autoJson = "";
    private string _explicitAutoJson = "";
    private string _typedJson = "";
    private string _sharedJson = "";
    private string _recordJson = "";

    [GlobalSetup]
    public void Setup()
    {
        _auto = Build(ArrayFiller.Fill, new Types.EventJsonBench[N]);
        _explicitAuto = Build(ArrayFiller.Fill, new Types.EventJsonAuto[N]);
        _typed = Build(ArrayFiller.Fill, new Types.EventJsonTyped[N]);
        _shared = Build(ArrayFiller.Fill, new Types.EventJsonShared[N]);
        _record = Build(ArrayFiller.Fill, new Types.EventJsonRecord[N]);
        _autoJson = JsonSerializer.Serialize(_auto);
        _explicitAutoJson = JsonSerializer.Serialize(_explicitAuto);
        _typedJson = JsonSerializer.Serialize(_typed);
        _sharedJson = JsonSerializer.Serialize(_shared);
        _recordJson = JsonSerializer.Serialize(_record);
    }

    [Benchmark] public string SerializeAuto() => JsonSerializer.Serialize(_auto);
    [Benchmark] public string SerializeExplicitAuto() => JsonSerializer.Serialize(_explicitAuto);
    [Benchmark] public string SerializeTyped() => JsonSerializer.Serialize(_typed);
    [Benchmark] public string SerializeShared() => JsonSerializer.Serialize(_shared);
    [Benchmark] public string SerializeRecord() => JsonSerializer.Serialize(_record);

    [Benchmark] public Types.EventJsonBench[]? DeserializeAuto() => JsonSerializer.Deserialize<Types.EventJsonBench[]>(_autoJson);
    [Benchmark] public Types.EventJsonAuto[]? DeserializeExplicitAuto() => JsonSerializer.Deserialize<Types.EventJsonAuto[]>(_explicitAutoJson);
    [Benchmark] public Types.EventJsonTyped[]? DeserializeTyped() => JsonSerializer.Deserialize<Types.EventJsonTyped[]>(_typedJson);
    [Benchmark] public Types.EventJsonShared[]? DeserializeShared() => JsonSerializer.Deserialize<Types.EventJsonShared[]>(_sharedJson);
    [Benchmark] public Types.EventJsonRecord[]? DeserializeRecord() => JsonSerializer.Deserialize<Types.EventJsonRecord[]>(_recordJson);

    [Benchmark] public Types.EventJsonBench[]? RoundTripAuto() => JsonSerializer.Deserialize<Types.EventJsonBench[]>(JsonSerializer.Serialize(_auto));
    [Benchmark] public Types.EventJsonAuto[]? RoundTripExplicitAuto() => JsonSerializer.Deserialize<Types.EventJsonAuto[]>(JsonSerializer.Serialize(_explicitAuto));
    [Benchmark] public Types.EventJsonTyped[]? RoundTripTyped() => JsonSerializer.Deserialize<Types.EventJsonTyped[]>(JsonSerializer.Serialize(_typed));
    [Benchmark] public Types.EventJsonShared[]? RoundTripShared() => JsonSerializer.Deserialize<Types.EventJsonShared[]>(JsonSerializer.Serialize(_shared));
    [Benchmark] public Types.EventJsonRecord[]? RoundTripRecord() => JsonSerializer.Deserialize<Types.EventJsonRecord[]>(JsonSerializer.Serialize(_record));

    private static T[] Build<T>(Action<T[], Random, Distribution> fill, T[] values)
    {
        fill(values, new Random(42), Distribution.Uniform);
        return values;
    }
}
