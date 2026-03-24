using System.Collections.Immutable;
using Spire.Analyzers.Utils.FlowAnalysis;
using Xunit;

namespace Spire.Analyzers.Tests.FlowAnalysis;

public class TransferFunctionTests
{
    [Fact]
    public void DefaultAssignment_SetsAllFieldsToDefault()
    {
        var state = new VariableState(
            ImmutableArray.Create(InitState.Initialized, InitState.Initialized),
            KindState.Unknown, NullState.NotNull);

        var result = state.WithAllFields(InitState.Default);

        Assert.Equal(InitState.Default, result.InitState);
        Assert.All(result.FieldStates, f => Assert.Equal(InitState.Default, f));
    }

    [Fact]
    public void FieldWrite_SetsSpecificFieldToInitialized()
    {
        var state = new VariableState(
            ImmutableArray.Create(InitState.Default, InitState.Default),
            KindState.Unknown, NullState.NotNull);

        var result = state.WithFieldState(0, InitState.Initialized);

        Assert.Equal(InitState.Initialized, result.FieldStates[0]);
        Assert.Equal(InitState.Default, result.FieldStates[1]);
        Assert.Equal(InitState.MaybeDefault, result.InitState);
    }

    [Fact]
    public void AllFieldsWritten_PromotesToInitialized()
    {
        var state = new VariableState(
            ImmutableArray.Create(InitState.Default, InitState.Default),
            KindState.Unknown, NullState.NotNull);

        state = state.WithFieldState(0, InitState.Initialized);
        state = state.WithFieldState(1, InitState.Initialized);

        Assert.Equal(InitState.Initialized, state.InitState);
    }

    [Fact]
    public void ObjectCreationWithArgs_SetsAllFieldsInitialized()
    {
        var state = new VariableState(
            ImmutableArray.Create(InitState.Default, InitState.Default),
            KindState.Unknown, NullState.NotNull);

        var result = state.WithAllFields(InitState.Initialized);

        Assert.Equal(InitState.Initialized, result.InitState);
    }

    [Fact]
    public void UnknownAssignment_SetsAllFieldsToMaybeDefault()
    {
        var state = new VariableState(
            ImmutableArray.Create(InitState.Initialized, InitState.Initialized),
            KindState.Unknown, NullState.NotNull);

        var result = state.WithAllFields(InitState.MaybeDefault);

        Assert.Equal(InitState.MaybeDefault, result.InitState);
    }
}
