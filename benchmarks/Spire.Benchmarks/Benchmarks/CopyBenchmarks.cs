using BenchmarkDotNet.Attributes;

namespace Spire.Benchmarks;

[Config(typeof(SpireConfig))]
public class CopyBenchmarks
{
    [Params(BenchN.Default)]
    public int N { get; set; }

    EventAdditive[] _srcAdd = null!; EventAdditive[] _dstAdd = null!;
    EventBoxedFields[] _srcBF = null!; EventBoxedFields[] _dstBF = null!;
    EventBoxedTuple[] _srcBT = null!; EventBoxedTuple[] _dstBT = null!;
    EventOverlap[] _srcOv = null!; EventOverlap[] _dstOv = null!;
    EventUnsafeOverlap[] _srcUO = null!; EventUnsafeOverlap[] _dstUO = null!;
    EventRecord[] _srcRec = null!; EventRecord[] _dstRec = null!;
    EventClass[] _srcCls = null!; EventClass[] _dstCls = null!;

    [GlobalSetup]
    public void Setup()
    {
        _srcAdd = new EventAdditive[N]; _dstAdd = new EventAdditive[N];
        _srcBF = new EventBoxedFields[N]; _dstBF = new EventBoxedFields[N];
        _srcBT = new EventBoxedTuple[N]; _dstBT = new EventBoxedTuple[N];
        _srcOv = new EventOverlap[N]; _dstOv = new EventOverlap[N];
        _srcUO = new EventUnsafeOverlap[N]; _dstUO = new EventUnsafeOverlap[N];
        _srcRec = new EventRecord[N]; _dstRec = new EventRecord[N];
        _srcCls = new EventClass[N]; _dstCls = new EventClass[N];

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
