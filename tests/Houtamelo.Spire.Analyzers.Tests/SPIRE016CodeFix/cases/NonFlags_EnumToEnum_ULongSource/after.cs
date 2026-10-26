using Houtamelo.Spire;

public enum SourceULong : ulong
{
    Value = 0x8000000000000001UL,
}

[EnforceInitialization]
public enum TargetULong : ulong
{
    Low = 1,
    Value = 0x8000000000000001UL,
}

public static class Consumer
{
    public static TargetULong Convert(SourceULong value)
        => SpireEnum<TargetULong>.From((ulong)value);
}
