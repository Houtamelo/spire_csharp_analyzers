using Houtamelo.Spire;

public enum SourceLongConditional : long
{
    Low = 1,
    Value = 1099511627777L,
}

[EnforceInitialization]
public enum TargetLongConditional : long
{
    Low = 1,
    Value = 1099511627777L,
}

public static class Consumer
{
    public static int Calls { get; private set; }

    private static SourceLongConditional Read()
    {
        Calls++;
        return SourceLongConditional.Value;
    }

    private static SourceLongConditional Other()
    {
        Calls++;
        return SourceLongConditional.Low;
    }

    public static TargetLongConditional Convert(bool choose)
        => SpireEnum<TargetLongConditional>.From((long)(choose ? Read() : Other()));
}
