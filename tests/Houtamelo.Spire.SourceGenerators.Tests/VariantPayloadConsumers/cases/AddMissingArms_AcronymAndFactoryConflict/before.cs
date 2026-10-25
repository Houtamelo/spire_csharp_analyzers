using Houtamelo.Spire;

[DiscriminatedUnion]
public partial struct Naming
{
    [Variant] public static partial Naming run(int value);
    [Variant] public static partial Naming HTTPRequest(string url);
    [Variant] public static partial Naming Idle();
}

public static class Consumer
{
    public static int Match(Naming value) => value switch
    {
        { kind: Naming.Kind.Idle } => 0,
    };
}
