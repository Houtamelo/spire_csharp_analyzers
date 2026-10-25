using Houtamelo.Spire;

[DiscriminatedUnion]
public partial struct Command
{
    [Variant] public static partial Command Run(float speed);
    [Variant] public static partial Command Stop();
}

public static class Consumer
{
    public static int Match(Command command)
    {
        switch (command)
        {
            case { kind: Command.Kind.Run, run: { speed: var speed } }:
                return (int)speed;
        }

        return -1;
    }
}
