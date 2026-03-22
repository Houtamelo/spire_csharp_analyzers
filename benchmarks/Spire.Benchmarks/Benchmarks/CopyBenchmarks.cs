using BenchmarkDotNet.Attributes;
using Spire.Benchmarks.Helpers;

namespace Spire.Benchmarks;

[Config(typeof(SpireConfig))]
public class CopyBenchmarks
{
    [Params(BenchN.Default)]
    public int N { get; set; }

    Types.EventAdditive[] _srcAdd = null!; Types.EventAdditive[] _dstAdd = null!;
    Types.EventBoxedFields[] _srcBF = null!; Types.EventBoxedFields[] _dstBF = null!;
    Types.EventBoxedTuple[] _srcBT = null!; Types.EventBoxedTuple[] _dstBT = null!;
    Types.EventOverlap[] _srcOv = null!; Types.EventOverlap[] _dstOv = null!;
    Types.EventUnsafeOverlap[] _srcUO = null!; Types.EventUnsafeOverlap[] _dstUO = null!;
    Types.EventRecord[] _srcRec = null!; Types.EventRecord[] _dstRec = null!;
    Types.EventClass[] _srcCls = null!; Types.EventClass[] _dstCls = null!;

    [GlobalSetup]
    public void Setup()
    {
        _srcAdd = new Types.EventAdditive[N]; _dstAdd = new Types.EventAdditive[N];
        _srcBF = new Types.EventBoxedFields[N]; _dstBF = new Types.EventBoxedFields[N];
        _srcBT = new Types.EventBoxedTuple[N]; _dstBT = new Types.EventBoxedTuple[N];
        _srcOv = new Types.EventOverlap[N]; _dstOv = new Types.EventOverlap[N];
        _srcUO = new Types.EventUnsafeOverlap[N]; _dstUO = new Types.EventUnsafeOverlap[N];
        _srcRec = new Types.EventRecord[N]; _dstRec = new Types.EventRecord[N];
        _srcCls = new Types.EventClass[N]; _dstCls = new Types.EventClass[N];

        ArrayFiller.Fill(_srcAdd, new Random(42), Distribution.Uniform);
        ArrayFiller.Fill(_srcBF, new Random(42), Distribution.Uniform);
        ArrayFiller.Fill(_srcBT, new Random(42), Distribution.Uniform);
        ArrayFiller.Fill(_srcOv, new Random(42), Distribution.Uniform);
        ArrayFiller.Fill(_srcUO, new Random(42), Distribution.Uniform);
        ArrayFiller.Fill(_srcRec, new Random(42), Distribution.Uniform);
        ArrayFiller.Fill(_srcCls, new Random(42), Distribution.Uniform);
    }

    [BenchmarkCategory("Copy"), Benchmark(Baseline = true, Description = "additive")]
    public void Additive() => Array.Copy(_srcAdd, _dstAdd, N);

    [BenchmarkCategory("Copy"), Benchmark(Description = "boxedFields")]
    public void BoxedFields() => Array.Copy(_srcBF, _dstBF, N);

    [BenchmarkCategory("Copy"), Benchmark(Description = "boxedTuple")]
    public void BoxedTuple() => Array.Copy(_srcBT, _dstBT, N);

    [BenchmarkCategory("Copy"), Benchmark(Description = "overlap")]
    public void Overlap() => Array.Copy(_srcOv, _dstOv, N);

    [BenchmarkCategory("Copy"), Benchmark(Description = "unsafeOverlap")]
    public void UnsafeOverlap() => Array.Copy(_srcUO, _dstUO, N);

    [BenchmarkCategory("Copy"), Benchmark(Description = "record")]
    public void Record() => Array.Copy(_srcRec, _dstRec, N);

    [BenchmarkCategory("Copy"), Benchmark(Description = "class")]
    public void Class() => Array.Copy(_srcCls, _dstCls, N);
}
