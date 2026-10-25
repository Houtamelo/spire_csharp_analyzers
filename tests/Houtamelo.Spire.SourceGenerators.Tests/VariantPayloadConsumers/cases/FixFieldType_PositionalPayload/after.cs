using Houtamelo.Spire;

[DiscriminatedUnion]
public partial struct MoveCommand
{
    [Variant] public static partial MoveCommand Move(int destination, float speed);
}

public static class Consumer
{
    public static int Match(MoveCommand command) => command switch
    {
        { kind: MoveCommand.Kind.Move, move: (int destination, float speed) } => 1,
    };
}
