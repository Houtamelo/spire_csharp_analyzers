using BenchmarkDotNet.Attributes;

namespace Houtamelo.Spire.Benchmarks.Benchmarks;

/// Single factory call per strategy. DisassemblyDiagnoser reveals JIT inlining.
[Config(typeof(SpireDisasmConfig))]
public class MicroConstructBenchmarks
{
    // ── Fieldless (Point/Idle) ──

    [BenchmarkCategory("Micro Construct Fieldless"), Benchmark(Baseline = true, Description = "additive")]
    public Types.EventAdditive FieldlessAdditive() => Types.EventAdditive.Point();

    [BenchmarkCategory("Micro Construct Fieldless"), Benchmark(Description = "boxedFields")]
    public Types.EventBoxedFields FieldlessBoxedFields() => Types.EventBoxedFields.Point();

    [BenchmarkCategory("Micro Construct Fieldless"), Benchmark(Description = "boxedTuple")]
    public Types.EventBoxedTuple FieldlessBoxedTuple() => Types.EventBoxedTuple.Point();

    [BenchmarkCategory("Micro Construct Fieldless"), Benchmark(Description = "overlap")]
    public Types.EventOverlap FieldlessOverlap() => Types.EventOverlap.Point();

    [BenchmarkCategory("Micro Construct Fieldless"), Benchmark(Description = "unsafeOverlap")]
    public Types.EventUnsafeOverlap FieldlessUnsafeOverlap() => Types.EventUnsafeOverlap.Point();

    [BenchmarkCategory("Micro Construct Fieldless"), Benchmark(Description = "record")]
    public Types.EventRecord FieldlessRecord() => new Types.EventRecord.Point();

    [BenchmarkCategory("Micro Construct Fieldless"), Benchmark(Description = "class")]
    public Types.EventClass FieldlessClass() => new Types.EventClass.Point();

    // ── 4 unmanaged fields (Transform) ──

    [BenchmarkCategory("Micro Construct 4-field"), Benchmark(Baseline = true, Description = "additive")]
    public Types.EventAdditive Transform4Additive() => Types.EventAdditive.Transform(1f, 2f, 3f, 4f);

    [BenchmarkCategory("Micro Construct 4-field"), Benchmark(Description = "boxedFields")]
    public Types.EventBoxedFields Transform4BoxedFields() => Types.EventBoxedFields.Transform(1f, 2f, 3f, 4f);

    [BenchmarkCategory("Micro Construct 4-field"), Benchmark(Description = "boxedTuple")]
    public Types.EventBoxedTuple Transform4BoxedTuple() => Types.EventBoxedTuple.Transform(1f, 2f, 3f, 4f);

    [BenchmarkCategory("Micro Construct 4-field"), Benchmark(Description = "overlap")]
    public Types.EventOverlap Transform4Overlap() => Types.EventOverlap.Transform(1f, 2f, 3f, 4f);

    [BenchmarkCategory("Micro Construct 4-field"), Benchmark(Description = "unsafeOverlap")]
    public Types.EventUnsafeOverlap Transform4UnsafeOverlap() => Types.EventUnsafeOverlap.Transform(1f, 2f, 3f, 4f);

    [BenchmarkCategory("Micro Construct 4-field"), Benchmark(Description = "record")]
    public Types.EventRecord Transform4Record() => new Types.EventRecord.Transform(1f, 2f, 3f, 4f);

    [BenchmarkCategory("Micro Construct 4-field"), Benchmark(Description = "class")]
    public Types.EventClass Transform4Class() => new Types.EventClass.Transform(1f, 2f, 3f, 4f);

    // ── 5 mixed fields (RichText) ──

    [BenchmarkCategory("Micro Construct 5-mixed"), Benchmark(Baseline = true, Description = "additive")]
    public Types.EventAdditive RichText5Additive() => Types.EventAdditive.RichText("hello", 12, true, "Arial", 1.5);

    [BenchmarkCategory("Micro Construct 5-mixed"), Benchmark(Description = "boxedFields")]
    public Types.EventBoxedFields RichText5BoxedFields() => Types.EventBoxedFields.RichText("hello", 12, true, "Arial", 1.5);

    [BenchmarkCategory("Micro Construct 5-mixed"), Benchmark(Description = "boxedTuple")]
    public Types.EventBoxedTuple RichText5BoxedTuple() => Types.EventBoxedTuple.RichText("hello", 12, true, "Arial", 1.5);

    [BenchmarkCategory("Micro Construct 5-mixed"), Benchmark(Description = "overlap")]
    public Types.EventOverlap RichText5Overlap() => Types.EventOverlap.RichText("hello", 12, true, "Arial", 1.5);

    [BenchmarkCategory("Micro Construct 5-mixed"), Benchmark(Description = "unsafeOverlap")]
    public Types.EventUnsafeOverlap RichText5UnsafeOverlap() => Types.EventUnsafeOverlap.RichText("hello", 12, true, "Arial", 1.5);

    [BenchmarkCategory("Micro Construct 5-mixed"), Benchmark(Description = "record")]
    public Types.EventRecord RichText5Record() => new Types.EventRecord.RichText("hello", 12, true, "Arial", 1.5);

    [BenchmarkCategory("Micro Construct 5-mixed"), Benchmark(Description = "class")]
    public Types.EventClass RichText5Class() => new Types.EventClass.RichText("hello", 12, true, "Arial", 1.5);
}
