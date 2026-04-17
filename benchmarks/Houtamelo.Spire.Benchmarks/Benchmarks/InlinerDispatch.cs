using System;
using BenchmarkDotNet.Attributes;
using Houtamelo.Spire;
using Houtamelo.Spire.Benchmarks.Helpers;

namespace Houtamelo.Spire.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public partial class InlinerDispatch
{
    private int[] _data = Array.Empty<int>();

    [GlobalSetup]
    public void Setup()
    {
        _data = new int[BenchN.Default];
        for (int i = 0; i < _data.Length; i++) _data[i] = i;
    }

    [Benchmark(Baseline = true)]
    public int Direct()
    {
        int s = 0;
        foreach (var x in _data) s += x * 2;
        return s;
    }

    [Benchmark]
    public int DelegateCall()
    {
        int s = 0;
        Func<int, int> f = static x => x * 2;
        foreach (var x in _data) s += f(x);
        return s;
    }

    [Benchmark]
    public int InlinerStructCall()
    {
        int s = 0;
        var f = default(DoubleInliner);
        foreach (var x in _data) s += f.Invoke(x);
        return s;
    }

    [InlinerStruct]
    private static int Double(int x) => x * 2;
}
