using BenchmarkDotNet.Attributes;
using Houtamelo.Spire.Benchmarks.Helpers;
using Newtonsoft.Json;

namespace Houtamelo.Spire.Benchmarks.Benchmarks;

[Config(typeof(SpireConfig))]
public class JsonNsjBenchmarks
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
        _autoJson = JsonConvert.SerializeObject(_auto);
        _explicitAutoJson = JsonConvert.SerializeObject(_explicitAuto);
        _typedJson = JsonConvert.SerializeObject(_typed);
        _sharedJson = JsonConvert.SerializeObject(_shared);
        _recordJson = JsonConvert.SerializeObject(_record);
    }

    [Benchmark] public string SerializeAuto() => JsonConvert.SerializeObject(_auto);
    [Benchmark] public string SerializeExplicitAuto() => JsonConvert.SerializeObject(_explicitAuto);
    [Benchmark] public string SerializeTyped() => JsonConvert.SerializeObject(_typed);
    [Benchmark] public string SerializeShared() => JsonConvert.SerializeObject(_shared);
    [Benchmark] public string SerializeRecord() => JsonConvert.SerializeObject(_record);

    [Benchmark] public Types.EventJsonBench[]? DeserializeAuto() => JsonConvert.DeserializeObject<Types.EventJsonBench[]>(_autoJson);
    [Benchmark] public Types.EventJsonAuto[]? DeserializeExplicitAuto() => JsonConvert.DeserializeObject<Types.EventJsonAuto[]>(_explicitAutoJson);
    [Benchmark] public Types.EventJsonTyped[]? DeserializeTyped() => JsonConvert.DeserializeObject<Types.EventJsonTyped[]>(_typedJson);
    [Benchmark] public Types.EventJsonShared[]? DeserializeShared() => JsonConvert.DeserializeObject<Types.EventJsonShared[]>(_sharedJson);
    [Benchmark] public Types.EventJsonRecord[]? DeserializeRecord() => JsonConvert.DeserializeObject<Types.EventJsonRecord[]>(_recordJson);

    [Benchmark] public Types.EventJsonBench[]? RoundTripAuto() => JsonConvert.DeserializeObject<Types.EventJsonBench[]>(JsonConvert.SerializeObject(_auto));
    [Benchmark] public Types.EventJsonAuto[]? RoundTripExplicitAuto() => JsonConvert.DeserializeObject<Types.EventJsonAuto[]>(JsonConvert.SerializeObject(_explicitAuto));
    [Benchmark] public Types.EventJsonTyped[]? RoundTripTyped() => JsonConvert.DeserializeObject<Types.EventJsonTyped[]>(JsonConvert.SerializeObject(_typed));
    [Benchmark] public Types.EventJsonShared[]? RoundTripShared() => JsonConvert.DeserializeObject<Types.EventJsonShared[]>(JsonConvert.SerializeObject(_shared));
    [Benchmark] public Types.EventJsonRecord[]? RoundTripRecord() => JsonConvert.DeserializeObject<Types.EventJsonRecord[]>(JsonConvert.SerializeObject(_record));

    private static T[] Build<T>(Action<T[], Random, Distribution> fill, T[] values)
    {
        fill(values, new Random(42), Distribution.Uniform);
        return values;
    }
}
