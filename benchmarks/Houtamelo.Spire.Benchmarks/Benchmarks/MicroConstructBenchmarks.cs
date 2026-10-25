using BenchmarkDotNet.Attributes;

namespace Houtamelo.Spire.Benchmarks.Benchmarks;

[Config(typeof(SpireDisasmConfig))]
public class MicroConstructBenchmarks
{
    [BenchmarkCategory("Micro Construct Fieldless"), Benchmark(Baseline = true)]
    public Types.EventBench FieldlessAuto() => Types.EventBench.Point();

    [BenchmarkCategory("Micro Construct Fieldless"), Benchmark]
    public Types.EventAuto FieldlessExplicitAuto() => Types.EventAuto.Point();

    [BenchmarkCategory("Micro Construct Fieldless"), Benchmark]
    public Types.EventTyped FieldlessTyped() => Types.EventTyped.Point();

    [BenchmarkCategory("Micro Construct Fieldless"), Benchmark]
    public Types.EventShared FieldlessShared() => Types.EventShared.Point();

    [BenchmarkCategory("Micro Construct Fieldless"), Benchmark]
    public Types.EventRecord FieldlessRecord() => new Types.EventRecord.Point();

    [BenchmarkCategory("Micro Construct Fieldless"), Benchmark]
    public Types.EventNative FieldlessNative() => new Types.EvtPoint();

    [BenchmarkCategory("Micro Construct Transform"), Benchmark(Baseline = true)]
    public Types.EventBench TransformAuto() => Types.EventBench.Transform(1f, 2f, 3f, 4f);

    [BenchmarkCategory("Micro Construct Transform"), Benchmark]
    public Types.EventAuto TransformExplicitAuto() => Types.EventAuto.Transform(1f, 2f, 3f, 4f);

    [BenchmarkCategory("Micro Construct Transform"), Benchmark]
    public Types.EventTyped TransformTyped() => Types.EventTyped.Transform(1f, 2f, 3f, 4f);

    [BenchmarkCategory("Micro Construct Transform"), Benchmark]
    public Types.EventShared TransformShared() => Types.EventShared.Transform(1f, 2f, 3f, 4f);

    [BenchmarkCategory("Micro Construct Transform"), Benchmark]
    public Types.EventRecord TransformRecord() => new Types.EventRecord.Transform(1f, 2f, 3f, 4f);

    [BenchmarkCategory("Micro Construct Transform"), Benchmark]
    public Types.EventNative TransformNative() => new Types.EvtTransform(1f, 2f, 3f, 4f);

    [BenchmarkCategory("Micro Construct RichText"), Benchmark(Baseline = true)]
    public Types.EventBench RichTextAuto() => Types.EventBench.RichText("hello", 12, true, "Arial", 1.5);

    [BenchmarkCategory("Micro Construct RichText"), Benchmark]
    public Types.EventAuto RichTextExplicitAuto() => Types.EventAuto.RichText("hello", 12, true, "Arial", 1.5);

    [BenchmarkCategory("Micro Construct RichText"), Benchmark]
    public Types.EventTyped RichTextTyped() => Types.EventTyped.RichText("hello", 12, true, "Arial", 1.5);

    [BenchmarkCategory("Micro Construct RichText"), Benchmark]
    public Types.EventShared RichTextShared() => Types.EventShared.RichText("hello", 12, true, "Arial", 1.5);

    [BenchmarkCategory("Micro Construct RichText"), Benchmark]
    public Types.EventRecord RichTextRecord() => new Types.EventRecord.RichText("hello", 12, true, "Arial", 1.5);

    [BenchmarkCategory("Micro Construct RichText"), Benchmark]
    public Types.EventNative RichTextNative() => new Types.EvtRichText("hello", 12, true, "Arial", 1.5);
}
