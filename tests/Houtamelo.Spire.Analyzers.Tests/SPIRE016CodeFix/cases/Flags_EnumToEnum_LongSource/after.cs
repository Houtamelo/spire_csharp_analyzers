using System;
using Houtamelo.Spire;

[Flags]
public enum SourceLongFlags : long
{
    None = 0,
    Read = 1099511627776L,
    Combined = 1099511627777L,
}

[EnforceInitialization]
[Flags]
public enum TargetLongFlags : long
{
    None = 0,
    Low = 1,
    Read = 1099511627776L,
    Combined = 1099511627777L,
}

public static class Consumer
{
    public static TargetLongFlags Convert(SourceLongFlags value)
        => SpireEnum<TargetLongFlags>.FromFlags((long)value);
}
