using Houtamelo.Spire;

public enum SourceSByte : sbyte
{
    Value = 100,
}

[EnforceInitialization]
public enum TargetSByte : sbyte
{
    Value = 100,
}

public static class Consumer
{
    public static TargetSByte Convert(SourceSByte value)
        => (TargetSByte)value;
}
