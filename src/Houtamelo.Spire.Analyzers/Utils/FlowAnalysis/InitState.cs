namespace Houtamelo.Spire.Analyzers.Utils.FlowAnalysis;

public enum InitState : byte
{
    Default = 0,
    Initialized = 1,
    MaybeDefault = 2,
}

public static class InitStateOps
{
    public static InitState Merge(InitState a, InitState b)
    {
        if (a == b) return a;
        return InitState.MaybeDefault;
    }
}
