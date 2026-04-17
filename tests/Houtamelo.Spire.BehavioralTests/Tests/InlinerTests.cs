using System.Collections.Generic;
using Houtamelo.Spire;
using Spire.BehavioralTests.Types;
using Xunit;

namespace Spire.BehavioralTests.Tests;

public class InlinerTests
{
    // ── [InlinerStruct] — generated struct forwards to the attributed method ──

    [Fact]
    public void InlinerStruct_ForwardsStaticCall()
    {
        var doubler = default(InlinerTargets.DoubleInliner);
        Assert.Equal(10, doubler.Invoke(5));
    }

    [Fact]
    public void InlinerStruct_ApplyMonomorphizes()
    {
        var result = InlinerHosts.Apply(7, default(InlinerTargets.DoubleInliner));
        Assert.Equal(14, result);
    }

    [Fact]
    public void InlinerStruct_ForwardsInstanceCallViaPositional()
    {
        var bucket = new List<int>();
        var accumulator = default(InlinerTargets.AccumulateInliner);
        accumulator.Invoke(bucket, 3);
        accumulator.Invoke(bucket, 4);

        Assert.Equal(new[] { 30, 40 }, bucket);
    }

    [Fact]
    public void InlinerStruct_LogNamedCompilesAndCallsThrough()
    {
        // Empty-body forwarder — just prove the generated struct satisfies the
        // IActionInliner<string, int> shape and Invoke is callable without throwing.
        var log = default(InlinerTargets.LogNamedInliner);
        log.Invoke("answer", 42);
    }

    // ── [Inlinable] twins — routed by overload resolution when caller passes a struct inliner ──

    [Fact]
    public void InlinableTwin_RoutedByOverloadResolution()
    {
        var bucket = new List<int>();
        var list = new List<int> { 1, 2, 3 };

        InlinerHosts.ForEach(list, new BoundAccumulator(bucket));

        Assert.Equal(new[] { 1, 2, 3 }, bucket);
    }

    [Fact]
    public void InlinableTwin_FuncReduce()
    {
        var list = new List<int> { 1, 2, 3, 4 };
        var sum = InlinerHosts.Reduce(list, 0, default(AddFold));
        Assert.Equal(10, sum);

        var product = InlinerHosts.Reduce(list, 1, default(MulFold));
        Assert.Equal(24, product);
    }

    // ── Hand-written inliner structs used in the [Inlinable] twin tests ──

    private readonly struct BoundAccumulator : IActionInliner<int>
    {
        private readonly List<int> _bucket;
        public BoundAccumulator(List<int> bucket) => _bucket = bucket;
        public void Invoke(int a1) => _bucket.Add(a1);
    }

    private readonly struct AddFold : IFuncInliner<int, int, int>
    {
        public int Invoke(int acc, int item) => acc + item;
    }

    private readonly struct MulFold : IFuncInliner<int, int, int>
    {
        public int Invoke(int acc, int item) => acc * item;
    }
}
