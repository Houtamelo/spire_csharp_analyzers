using Spire.Analyzers.Utils.FlowAnalysis;
using Xunit;

namespace Spire.Analyzers.Tests.FlowAnalysis;

public class InitStateLatticeTests
{
    [Fact]
    public void Merge_SameValues_ReturnsSame()
    {
        Assert.Equal(InitState.Default, InitStateOps.Merge(InitState.Default, InitState.Default));
        Assert.Equal(InitState.Initialized, InitStateOps.Merge(InitState.Initialized, InitState.Initialized));
        Assert.Equal(InitState.MaybeDefault, InitStateOps.Merge(InitState.MaybeDefault, InitState.MaybeDefault));
    }

    [Fact]
    public void Merge_DifferentValues_ReturnsMaybeDefault()
    {
        Assert.Equal(InitState.MaybeDefault, InitStateOps.Merge(InitState.Default, InitState.Initialized));
        Assert.Equal(InitState.MaybeDefault, InitStateOps.Merge(InitState.Initialized, InitState.Default));
    }

    [Fact]
    public void Merge_WithMaybeDefault_ReturnsMaybeDefault()
    {
        Assert.Equal(InitState.MaybeDefault, InitStateOps.Merge(InitState.MaybeDefault, InitState.Default));
        Assert.Equal(InitState.MaybeDefault, InitStateOps.Merge(InitState.MaybeDefault, InitState.Initialized));
    }

    [Fact]
    public void Merge_IsCommutative()
    {
        foreach (var a in new[] { InitState.Default, InitState.Initialized, InitState.MaybeDefault })
        foreach (var b in new[] { InitState.Default, InitState.Initialized, InitState.MaybeDefault })
            Assert.Equal(InitStateOps.Merge(a, b), InitStateOps.Merge(b, a));
    }
}
