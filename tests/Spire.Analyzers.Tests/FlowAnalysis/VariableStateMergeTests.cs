// tests/Spire.Analyzers.Tests/FlowAnalysis/VariableStateMergeTests.cs
using System.Collections.Immutable;
using Spire.Analyzers.Utils.FlowAnalysis;
using Xunit;

namespace Spire.Analyzers.Tests.FlowAnalysis;

public class VariableStateMergeTests
{
    [Fact]
    public void Merge_BothDefault_ReturnsDefault()
    {
        var fields = ImmutableArray.Create(InitState.Default, InitState.Default);
        var a = new VariableState(fields, KindState.Unknown, NullState.NotNull);
        var b = new VariableState(fields, KindState.Unknown, NullState.NotNull);
        var merged = VariableState.Merge(a, b);

        Assert.Equal(InitState.Default, merged.InitState);
        Assert.All(merged.FieldStates, f => Assert.Equal(InitState.Default, f));
    }

    [Fact]
    public void Merge_OneFieldDiffers_VariableIsMaybeDefault()
    {
        var a = new VariableState(
            ImmutableArray.Create(InitState.Initialized, InitState.Default),
            KindState.Unknown, NullState.NotNull);
        var b = new VariableState(
            ImmutableArray.Create(InitState.Default, InitState.Initialized),
            KindState.Unknown, NullState.NotNull);
        var merged = VariableState.Merge(a, b);

        Assert.Equal(InitState.MaybeDefault, merged.InitState);
        Assert.Equal(InitState.MaybeDefault, merged.FieldStates[0]);
        Assert.Equal(InitState.MaybeDefault, merged.FieldStates[1]);
    }

    [Fact]
    public void Merge_AllFieldsInitialized_VariableIsInitialized()
    {
        var fields = ImmutableArray.Create(InitState.Initialized, InitState.Initialized);
        var a = new VariableState(fields, KindState.Unknown, NullState.NotNull);
        var b = new VariableState(fields, KindState.Unknown, NullState.NotNull);
        var merged = VariableState.Merge(a, b);

        Assert.Equal(InitState.Initialized, merged.InitState);
    }

    [Fact]
    public void Merge_EmptyFieldStates_InitStateBasedOnNullState()
    {
        var a = new VariableState(ImmutableArray<InitState>.Empty, KindState.Unknown, NullState.NotNull);
        var b = new VariableState(ImmutableArray<InitState>.Empty, KindState.Unknown, NullState.Null);
        var merged = VariableState.Merge(a, b);

        Assert.Equal(NullState.MaybeNull, merged.NullState);
    }

    [Fact]
    public void InitState_DerivedFromFieldStates()
    {
        var allDefault = new VariableState(
            ImmutableArray.Create(InitState.Default, InitState.Default),
            KindState.Unknown, NullState.NotNull);
        Assert.Equal(InitState.Default, allDefault.InitState);

        var allInit = new VariableState(
            ImmutableArray.Create(InitState.Initialized, InitState.Initialized),
            KindState.Unknown, NullState.NotNull);
        Assert.Equal(InitState.Initialized, allInit.InitState);

        var mixed = new VariableState(
            ImmutableArray.Create(InitState.Default, InitState.Initialized),
            KindState.Unknown, NullState.NotNull);
        Assert.Equal(InitState.MaybeDefault, mixed.InitState);
    }

    [Fact]
    public void Equality_SameState_ReturnsTrue()
    {
        var a = new VariableState(
            ImmutableArray.Create(InitState.Default),
            KindState.Known(ImmutableHashSet.Create("Circle")),
            NullState.NotNull);
        var b = new VariableState(
            ImmutableArray.Create(InitState.Default),
            KindState.Known(ImmutableHashSet.Create("Circle")),
            NullState.NotNull);
        Assert.Equal(a, b);
    }
}
