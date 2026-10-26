using Houtamelo.Spire;

public enum SourceInt
{
    Value = 1,
}

[EnforceInitialization]
public enum TargetInt
{
    Value = 1,
}

public static class Consumer
{
    public static TargetInt Convert(SourceInt value)
        => (TargetInt)value;
}
