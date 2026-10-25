using Houtamelo.Spire;

[DiscriminatedUnion]
public partial struct MoveCommand
{
    [Variant] public static partial MoveCommand Move(float speed, float distance);
}

public static class Consumer
{
    public static int Match(MoveCommand command) => command switch
    {
        { kind: MoveCommand.Kind.Move, move: { speed: string bad, distance: var distance } } => 1,
    };
}
