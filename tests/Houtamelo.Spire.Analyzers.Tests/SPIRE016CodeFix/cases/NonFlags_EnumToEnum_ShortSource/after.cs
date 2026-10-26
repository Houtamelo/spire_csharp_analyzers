using Houtamelo.Spire;

public enum SourceShort : short
{
    Value = 30000,
}

[EnforceInitialization]
public enum TargetShort : short
{
    Value = 30000,
}

public static class Consumer
{
    public static TargetShort Convert(SourceShort value)
        => SpireEnum<TargetShort>.From((short)value);
}
