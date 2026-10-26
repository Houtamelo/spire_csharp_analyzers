using Houtamelo.Spire;

public enum SourceByte : byte
{
    Value = 200,
}

[EnforceInitialization]
public enum TargetByte : byte
{
    Value = 200,
}

public static class Consumer
{
    public static TargetByte Convert(SourceByte value)
        => SpireEnum<TargetByte>.From((byte)value);
}
