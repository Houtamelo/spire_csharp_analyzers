using System.Collections.Immutable;
using Houtamelo.Spire.Analyzers.Utils.FlowAnalysis;
using Xunit;

namespace Spire.Analyzers.Tests.FlowAnalysis;

public class KindStateLatticeTests
{
    [Fact]
    public void Merge_TwoKnown_ReturnsUnion()
    {
        var a = KindState.Known(ImmutableHashSet.Create("Circle"));
        var b = KindState.Known(ImmutableHashSet.Create("Rectangle"));
        var merged = KindState.Merge(a, b);

        Assert.True(merged.IsKnown);
        Assert.Equal(2, merged.Variants!.Count);
        Assert.Contains("Circle", merged.Variants);
        Assert.Contains("Rectangle", merged.Variants);
    }

    [Fact]
    public void Merge_KnownAndUnknown_ReturnsUnknown()
    {
        var known = KindState.Known(ImmutableHashSet.Create("Circle"));
        var merged = KindState.Merge(known, KindState.Unknown);
        Assert.False(merged.IsKnown);
    }

    [Fact]
    public void Merge_UnknownAndUnknown_ReturnsUnknown()
    {
        var merged = KindState.Merge(KindState.Unknown, KindState.Unknown);
        Assert.False(merged.IsKnown);
    }

    [Fact]
    public void Merge_SameKnown_ReturnsSameSet()
    {
        var set = ImmutableHashSet.Create("Circle");
        var merged = KindState.Merge(KindState.Known(set), KindState.Known(set));
        Assert.True(merged.IsKnown);
        Assert.Single(merged.Variants!);
    }

    [Fact]
    public void Merge_IsCommutative()
    {
        var a = KindState.Known(ImmutableHashSet.Create("Circle"));
        var b = KindState.Known(ImmutableHashSet.Create("Rectangle"));
        Assert.Equal(KindState.Merge(a, b), KindState.Merge(b, a));
    }
}
