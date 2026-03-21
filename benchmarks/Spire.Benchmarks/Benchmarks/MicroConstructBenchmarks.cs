using BenchmarkDotNet.Attributes;

namespace Spire.Benchmarks;

/// Single factory call per strategy. DisassemblyDiagnoser reveals JIT inlining.
[Config(typeof(SpireDisasmConfig))]
public class MicroConstructBenchmarks
{
    // ── Fieldless (Point/Idle) ──

    [BenchmarkCategory("Micro Construct Fieldless"), Benchmark(Baseline = true, Description = "additive")]
    public EventAdditive FieldlessAdditive() => EventAdditive.Point();

    [BenchmarkCategory("Micro Construct Fieldless"), Benchmark(Description = "boxedFields")]
    public EventBoxedFields FieldlessBoxedFields() => EventBoxedFields.Point();

    [BenchmarkCategory("Micro Construct Fieldless"), Benchmark(Description = "boxedTuple")]
    public EventBoxedTuple FieldlessBoxedTuple() => EventBoxedTuple.Point();

    [BenchmarkCategory("Micro Construct Fieldless"), Benchmark(Description = "overlap")]
    public EventOverlap FieldlessOverlap() => EventOverlap.Point();

    [BenchmarkCategory("Micro Construct Fieldless"), Benchmark(Description = "unsafeOverlap")]
    public EventUnsafeOverlap FieldlessUnsafeOverlap() => EventUnsafeOverlap.Point();

    [BenchmarkCategory("Micro Construct Fieldless"), Benchmark(Description = "record")]
    public EventRecord FieldlessRecord() => new EventRecord.Point();

    [BenchmarkCategory("Micro Construct Fieldless"), Benchmark(Description = "class")]
    public EventClass FieldlessClass() => new EventClass.Point();

    // ── 4 unmanaged fields (Transform) ──

    [BenchmarkCategory("Micro Construct 4-field"), Benchmark(Baseline = true, Description = "additive")]
    public EventAdditive Transform4Additive() => EventAdditive.Transform(1f, 2f, 3f, 4f);

    [BenchmarkCategory("Micro Construct 4-field"), Benchmark(Description = "boxedFields")]
    public EventBoxedFields Transform4BoxedFields() => EventBoxedFields.Transform(1f, 2f, 3f, 4f);

    [BenchmarkCategory("Micro Construct 4-field"), Benchmark(Description = "boxedTuple")]
    public EventBoxedTuple Transform4BoxedTuple() => EventBoxedTuple.Transform(1f, 2f, 3f, 4f);

    [BenchmarkCategory("Micro Construct 4-field"), Benchmark(Description = "overlap")]
    public EventOverlap Transform4Overlap() => EventOverlap.Transform(1f, 2f, 3f, 4f);

    [BenchmarkCategory("Micro Construct 4-field"), Benchmark(Description = "unsafeOverlap")]
    public EventUnsafeOverlap Transform4UnsafeOverlap() => EventUnsafeOverlap.Transform(1f, 2f, 3f, 4f);

    [BenchmarkCategory("Micro Construct 4-field"), Benchmark(Description = "record")]
    public EventRecord Transform4Record() => new EventRecord.Transform(1f, 2f, 3f, 4f);

    [BenchmarkCategory("Micro Construct 4-field"), Benchmark(Description = "class")]
    public EventClass Transform4Class() => new EventClass.Transform(1f, 2f, 3f, 4f);

    // ── 5 mixed fields (RichText) ──

    [BenchmarkCategory("Micro Construct 5-mixed"), Benchmark(Baseline = true, Description = "additive")]
    public EventAdditive RichText5Additive() => EventAdditive.RichText("hello", 12, true, "Arial", 1.5);

    [BenchmarkCategory("Micro Construct 5-mixed"), Benchmark(Description = "boxedFields")]
    public EventBoxedFields RichText5BoxedFields() => EventBoxedFields.RichText("hello", 12, true, "Arial", 1.5);

    [BenchmarkCategory("Micro Construct 5-mixed"), Benchmark(Description = "boxedTuple")]
    public EventBoxedTuple RichText5BoxedTuple() => EventBoxedTuple.RichText("hello", 12, true, "Arial", 1.5);

    [BenchmarkCategory("Micro Construct 5-mixed"), Benchmark(Description = "overlap")]
    public EventOverlap RichText5Overlap() => EventOverlap.RichText("hello", 12, true, "Arial", 1.5);

    [BenchmarkCategory("Micro Construct 5-mixed"), Benchmark(Description = "unsafeOverlap")]
    public EventUnsafeOverlap RichText5UnsafeOverlap() => EventUnsafeOverlap.RichText("hello", 12, true, "Arial", 1.5);

    [BenchmarkCategory("Micro Construct 5-mixed"), Benchmark(Description = "record")]
    public EventRecord RichText5Record() => new EventRecord.RichText("hello", 12, true, "Arial", 1.5);

    [BenchmarkCategory("Micro Construct 5-mixed"), Benchmark(Description = "class")]
    public EventClass RichText5Class() => new EventClass.RichText("hello", 12, true, "Arial", 1.5);
}
