using Houtamelo.Spire;

public enum SourceUShort : ushort
{
    Value = 60000,
}

[EnforceInitialization]
public enum TargetUShort : ushort
{
    Value = 60000,
}

public static class Consumer
{
    public static TargetUShort Convert(SourceUShort value)
        => (TargetUShort)value;
}
