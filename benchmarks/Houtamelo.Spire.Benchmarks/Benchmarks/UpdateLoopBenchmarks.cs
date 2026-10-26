using BenchmarkDotNet.Attributes;
using Houtamelo.Spire.Benchmarks.Helpers;

namespace Houtamelo.Spire.Benchmarks.Benchmarks;

[Config(typeof(SpireConfig))]
public class UpdateLoopBenchmarks
{
    [Params(BenchN.Default)]
    public int N { get; set; }

    private Types.EventBench[] _auto = null!;
    private Types.EventAuto[] _explicitAuto = null!;
    private Types.EventTyped[] _typed = null!;
    private Types.EventShared[] _shared = null!;
    private Types.EventRecord[] _record = null!;
    private Types.EventNative[] _native = null!;

    [GlobalSetup]
    public void Setup()
    {
        _auto = new Types.EventBench[N];
        _explicitAuto = new Types.EventAuto[N];
        _typed = new Types.EventTyped[N];
        _shared = new Types.EventShared[N];
        _record = new Types.EventRecord[N];
        _native = new Types.EventNative[N];
        ArrayFiller.Fill(_auto, new Random(42), Distribution.Uniform);
        ArrayFiller.Fill(_explicitAuto, new Random(42), Distribution.Uniform);
        ArrayFiller.Fill(_typed, new Random(42), Distribution.Uniform);
        ArrayFiller.Fill(_shared, new Random(42), Distribution.Uniform);
        ArrayFiller.Fill(_record, new Random(42), Distribution.Uniform);
        ArrayFiller.Fill(_native, new Random(42), Distribution.Uniform);
    }

    [BenchmarkCategory("Update"), Benchmark(Baseline = true)]
    public double Auto() => Update(_auto);

    [BenchmarkCategory("Update"), Benchmark]
    public double ExplicitAuto() => Update(_explicitAuto);

    [BenchmarkCategory("Update"), Benchmark]
    public double Typed() => Update(_typed);

    [BenchmarkCategory("Update"), Benchmark]
    public double Shared() => Update(_shared);

    [BenchmarkCategory("Update"), Benchmark]
    public double Record() => Update(_record);

    [BenchmarkCategory("Update"), Benchmark]
    public double Native() => Update(_native);

    private static double Update(Types.EventBench[] values)
    {
        double sum = 0;
        for (var index = 0; index < values.Length; index++)
        {
            var value = values[index];
            switch (value.kind)
            {
                case Types.EventBench.Kind.Point:
                case Types.EventBench.Kind.Label:
                case Types.EventBench.Kind.Error:
                    break;
                case Types.EventBench.Kind.Circle: sum += value.circle.radius; break;
                case Types.EventBench.Kind.Rectangle: sum += value.rectangle.width * value.rectangle.height; break;
                case Types.EventBench.Kind.Transform: sum += value.transform.x + value.transform.y; break;
                case Types.EventBench.Kind.RichText: sum += value.richText.size; break;
                case Types.EventBench.Kind.ColoredLine: sum += value.coloredLine.x1 + value.coloredLine.y1; break;
            }
        }
        return sum;
    }

    private static double Update(Types.EventAuto[] values)
    {
        double sum = 0;
        for (var index = 0; index < values.Length; index++)
        {
            var value = values[index];
            switch (value.kind)
            {
                case Types.EventAuto.Kind.Point:
                case Types.EventAuto.Kind.Label:
                case Types.EventAuto.Kind.Error:
                    break;
                case Types.EventAuto.Kind.Circle: sum += value.circle.radius; break;
                case Types.EventAuto.Kind.Rectangle: sum += value.rectangle.width * value.rectangle.height; break;
                case Types.EventAuto.Kind.Transform: sum += value.transform.x + value.transform.y; break;
                case Types.EventAuto.Kind.RichText: sum += value.richText.size; break;
                case Types.EventAuto.Kind.ColoredLine: sum += value.coloredLine.x1 + value.coloredLine.y1; break;
            }
        }
        return sum;
    }

    private static double Update(Types.EventTyped[] values)
    {
        double sum = 0;
        for (var index = 0; index < values.Length; index++)
        {
            var value = values[index];
            switch (value.kind)
            {
                case Types.EventTyped.Kind.Point:
                case Types.EventTyped.Kind.Label:
                case Types.EventTyped.Kind.Error:
                    break;
                case Types.EventTyped.Kind.Circle: sum += value.circle.radius; break;
                case Types.EventTyped.Kind.Rectangle: sum += value.rectangle.width * value.rectangle.height; break;
                case Types.EventTyped.Kind.Transform: sum += value.transform.x + value.transform.y; break;
                case Types.EventTyped.Kind.RichText: sum += value.richText.size; break;
                case Types.EventTyped.Kind.ColoredLine: sum += value.coloredLine.x1 + value.coloredLine.y1; break;
            }
        }
        return sum;
    }

    private static double Update(Types.EventShared[] values)
    {
        double sum = 0;
        for (var index = 0; index < values.Length; index++)
        {
            var value = values[index];
            switch (value.kind)
            {
                case Types.EventShared.Kind.Point:
                case Types.EventShared.Kind.Label:
                case Types.EventShared.Kind.Error:
                    break;
                case Types.EventShared.Kind.Circle: sum += value.circle.radius; break;
                case Types.EventShared.Kind.Rectangle: sum += value.rectangle.width * value.rectangle.height; break;
                case Types.EventShared.Kind.Transform: sum += value.transform.x + value.transform.y; break;
                case Types.EventShared.Kind.RichText: sum += value.richText.size; break;
                case Types.EventShared.Kind.ColoredLine: sum += value.coloredLine.x1 + value.coloredLine.y1; break;
            }
        }
        return sum;
    }

    private static double Update(Types.EventRecord[] values)
    {
        double sum = 0;
        foreach (var value in values)
        {
            sum += value switch
            {
                Types.EventRecord.Circle(var radius) => radius,
                Types.EventRecord.Rectangle(var width, var height) => width * height,
                Types.EventRecord.Transform(var x, var y, _, _) => x + y,
                Types.EventRecord.RichText(_, var size, _, _, _) => size,
                Types.EventRecord.ColoredLine(var x, var y, _) => x + y,
                _ => 0,
            };
        }
        return sum;
    }

    private static double Update(Types.EventNative[] values)
    {
        double sum = 0;
        foreach (var value in values)
        {
            sum += value switch
            {
                Types.EvtCircle(var radius) => radius,
                Types.EvtRectangle(var width, var height) => width * height,
                Types.EvtTransform(var x, var y, _, _) => x + y,
                Types.EvtRichText(_, var size, _, _, _) => size,
                Types.EvtColoredLine(var x, var y, _) => x + y,
                _ => 0,
            };
        }
        return sum;
    }
}
