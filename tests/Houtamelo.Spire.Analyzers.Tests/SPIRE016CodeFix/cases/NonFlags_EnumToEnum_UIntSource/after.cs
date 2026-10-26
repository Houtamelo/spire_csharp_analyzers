using Houtamelo.Spire;

public enum SourceUInt : uint
{
    Value = 4000000000U,
}

[EnforceInitialization]
public enum TargetUInt : uint
{
    Value = 4000000000U,
}

public static class Consumer
{
    public static TargetUInt Convert(SourceUInt value)
        => SpireEnum<TargetUInt>.From((uint)value);
}
