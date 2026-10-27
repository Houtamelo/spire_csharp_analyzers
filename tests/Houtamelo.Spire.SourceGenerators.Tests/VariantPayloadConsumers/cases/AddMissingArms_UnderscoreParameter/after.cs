using Houtamelo.Spire;

[DiscriminatedUnion]
public partial struct Command
{
    [Variant] public static partial Command Some(int _);
    [Variant] public static partial Command None();
}

public static class Consumer
{
    public static int Match(Command command) => command switch
    {
        { kind: Command.Kind.None } => 0,
        { IsSome: true, some: var (_1) } => throw new System.NotImplementedException()
    };
}
