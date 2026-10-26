using Houtamelo.Spire;

public enum SourceLong : long
{
    Value = 1099511627777L,
}

[EnforceInitialization]
public enum TargetLong : long
{
    Low = 1,
    Value = 1099511627777L,
}

public static class Consumer
{
    public static int Calls { get; private set; }

    private static SourceLong Read()
    {
        Calls++;
        return SourceLong.Value;
    }

    public static TargetLong Convert()
        => SpireEnum<TargetLong>.From((long)Read());
}
