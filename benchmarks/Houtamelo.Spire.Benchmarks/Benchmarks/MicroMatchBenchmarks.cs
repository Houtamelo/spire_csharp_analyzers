using BenchmarkDotNet.Attributes;

namespace Houtamelo.Spire.Benchmarks.Benchmarks;

[Config(typeof(SpireDisasmConfig))]
public class MicroMatchBenchmarks
{
    private Types.EventBench _auto;
    private Types.EventAuto _explicitAuto;
    private Types.EventTyped _typed;
    private Types.EventShared _shared;
    private Types.EventRecord _record = new Types.EventRecord.Transform(1f, 2f, 3f, 4f);
    private Types.EventNative _native = new Types.EvtTransform(1f, 2f, 3f, 4f);

    [GlobalSetup]
    public void Setup()
    {
        _auto = Types.EventBench.Transform(1f, 2f, 3f, 4f);
        _explicitAuto = Types.EventAuto.Transform(1f, 2f, 3f, 4f);
        _typed = Types.EventTyped.Transform(1f, 2f, 3f, 4f);
        _shared = Types.EventShared.Transform(1f, 2f, 3f, 4f);
    }

    [Benchmark(Baseline = true)] public double Auto() => _auto.transform.x * _auto.transform.y + _auto.transform.z * _auto.transform.w;
    [Benchmark] public double ExplicitAuto() => _explicitAuto.transform.x * _explicitAuto.transform.y + _explicitAuto.transform.z * _explicitAuto.transform.w;
    [Benchmark] public double Typed() => _typed.transform.x * _typed.transform.y + _typed.transform.z * _typed.transform.w;
    [Benchmark] public double Shared() => _shared.transform.x * _shared.transform.y + _shared.transform.z * _shared.transform.w;
    [Benchmark] public double Record() => _record switch { Types.EventRecord.Transform(var x, var y, var z, var w) => x * y + z * w, _ => 0 };
    [Benchmark] public double Native() => _native switch { Types.EvtTransform(var x, var y, var z, var w) => x * y + z * w, _ => 0 };
}
