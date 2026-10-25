using Houtamelo.Spire;

[DiscriminatedUnion]
public partial struct KeywordCommand
{
    [Variant] public static partial KeywordCommand Run(string @class, int count);
}

public static class Consumer
{
    public static int Match(KeywordCommand command) => command switch
    {
        { kind: KeywordCommand.Kind.Run, run: { @class: var @class, count: var count } } => count,
    };
}
