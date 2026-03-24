using Spire.Analyzers.Utils.FlowAnalysis;
using Xunit;

namespace Spire.Analyzers.Tests.FlowAnalysis;

public class NullStateLatticeTests
{
    [Fact]
    public void Merge_SameValues_ReturnsSame()
    {
        Assert.Equal(NullState.Null, NullStateOps.Merge(NullState.Null, NullState.Null));
        Assert.Equal(NullState.NotNull, NullStateOps.Merge(NullState.NotNull, NullState.NotNull));
        Assert.Equal(NullState.MaybeNull, NullStateOps.Merge(NullState.MaybeNull, NullState.MaybeNull));
    }

    [Fact]
    public void Merge_NullAndNotNull_ReturnsMaybeNull()
    {
        Assert.Equal(NullState.MaybeNull, NullStateOps.Merge(NullState.Null, NullState.NotNull));
        Assert.Equal(NullState.MaybeNull, NullStateOps.Merge(NullState.NotNull, NullState.Null));
    }

    [Fact]
    public void Merge_IsCommutative()
    {
        foreach (var a in new[] { NullState.Null, NullState.NotNull, NullState.MaybeNull })
        foreach (var b in new[] { NullState.Null, NullState.NotNull, NullState.MaybeNull })
            Assert.Equal(NullStateOps.Merge(a, b), NullStateOps.Merge(b, a));
    }
}
