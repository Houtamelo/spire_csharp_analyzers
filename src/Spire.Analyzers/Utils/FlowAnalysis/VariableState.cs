using System;
using System.Collections.Immutable;

namespace Spire.Analyzers.Utils.FlowAnalysis;

public readonly struct VariableState : IEquatable<VariableState>
{
    public ImmutableArray<InitState> FieldStates { get; }
    public KindState KindState { get; }
    public NullState NullState { get; }

    /// Derived from FieldStates. If FieldStates is empty, returns Initialized (no fields to track).
    public InitState InitState
    {
        get
        {
            if (FieldStates.IsDefaultOrEmpty)
                return InitState.Initialized;

            var hasDefault = false;
            var hasInitialized = false;
            foreach (var f in FieldStates)
            {
                if (f == InitState.MaybeDefault) return InitState.MaybeDefault;
                if (f == InitState.Default) hasDefault = true;
                else hasInitialized = true;
            }

            if (hasDefault && hasInitialized) return InitState.MaybeDefault;
            return hasDefault ? InitState.Default : InitState.Initialized;
        }
    }

    public VariableState(ImmutableArray<InitState> fieldStates, KindState kindState, NullState nullState)
    {
        FieldStates = fieldStates;
        KindState = kindState;
        NullState = nullState;
    }

    public VariableState WithFieldState(int ordinal, InitState state)
    {
        var builder = FieldStates.ToBuilder();
        builder[ordinal] = state;
        return new VariableState(builder.ToImmutable(), KindState, NullState);
    }

    public VariableState WithAllFields(InitState state)
    {
        if (FieldStates.IsDefaultOrEmpty)
            return this;
        var builder = ImmutableArray.CreateBuilder<InitState>(FieldStates.Length);
        for (int i = 0; i < FieldStates.Length; i++)
            builder.Add(state);
        return new VariableState(builder.MoveToImmutable(), KindState, NullState);
    }

    public VariableState WithKindState(KindState kindState)
        => new(FieldStates, kindState, NullState);

    public VariableState WithNullState(NullState nullState)
        => new(FieldStates, KindState, nullState);

    public static VariableState Merge(VariableState a, VariableState b)
    {
        var mergedKind = KindState.Merge(a.KindState, b.KindState);
        var mergedNull = NullStateOps.Merge(a.NullState, b.NullState);

        if (a.FieldStates.IsDefaultOrEmpty && b.FieldStates.IsDefaultOrEmpty)
            return new VariableState(ImmutableArray<InitState>.Empty, mergedKind, mergedNull);

        var length = a.FieldStates.Length;
        var builder = ImmutableArray.CreateBuilder<InitState>(length);
        for (int i = 0; i < length; i++)
            builder.Add(InitStateOps.Merge(a.FieldStates[i], b.FieldStates[i]));

        return new VariableState(builder.MoveToImmutable(), mergedKind, mergedNull);
    }

    public bool Equals(VariableState other)
    {
        if (KindState != other.KindState) return false;
        if (NullState != other.NullState) return false;
        if (FieldStates.Length != other.FieldStates.Length) return false;
        for (int i = 0; i < FieldStates.Length; i++)
            if (FieldStates[i] != other.FieldStates[i]) return false;
        return true;
    }

    public override bool Equals(object? obj) => obj is VariableState other && Equals(other);

    public override int GetHashCode()
    {
        var hash = ((int)NullState * 397) ^ KindState.GetHashCode();
        foreach (var f in FieldStates)
            hash = (hash * 397) ^ (int)f;
        return hash;
    }

    public static bool operator ==(VariableState left, VariableState right) => left.Equals(right);
    public static bool operator !=(VariableState left, VariableState right) => !left.Equals(right);
}
