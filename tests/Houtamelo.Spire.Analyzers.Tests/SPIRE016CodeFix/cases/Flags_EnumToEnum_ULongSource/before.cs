using System;
using Houtamelo.Spire;

[Flags]
public enum SourceULongFlags : ulong
{
    None = 0,
    High = 0x8000000000000000UL,
    Combined = 0x8000000000000001UL,
}

[EnforceInitialization]
[Flags]
public enum TargetULongFlags : ulong
{
    None = 0,
    Low = 1,
    High = 0x8000000000000000UL,
    Combined = 0x8000000000000001UL,
}

public static class Consumer
{
    public static TargetULongFlags Convert(SourceULongFlags value)
        => (TargetULongFlags)value;
}
