using Houtamelo.Spire;

[DiscriminatedUnion]
partial struct Command
{
    [Variant] public static partial Command Move(float speed);
    [Variant] public static partial Command Stop();
}

class Consumer
{
    int Match(Command command)
    {
        switch (command)
        {
            case { kind: Command.Kind.Move, move: { speed: var speed } }:
                return (int)speed;
        }

        return -1;
    }
}
