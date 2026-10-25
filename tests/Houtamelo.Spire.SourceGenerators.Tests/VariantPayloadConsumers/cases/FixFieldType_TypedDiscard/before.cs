using Houtamelo.Spire;

[DiscriminatedUnion]
public partial struct CountCommand
{
    [Variant] public static partial CountCommand Run(int count);
}

public static class Consumer
{
    public static int Match(CountCommand command) => command switch
    {
        { kind: CountCommand.Kind.Run, run: { count: string _ } } => 1,
    };
}
