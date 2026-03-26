namespace Houtamelo.Spire.Analyzers.Utils.FlowAnalysis;

public enum NullState : byte
{
    NotNull = 0,
    Null = 1,
    MaybeNull = 2,
}

public static class NullStateOps
{
    public static NullState Merge(NullState a, NullState b)
    {
        if (a == b) return a;
        return NullState.MaybeNull;
    }
}
