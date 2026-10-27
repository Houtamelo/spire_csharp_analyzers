using Houtamelo.Spire;

[DiscriminatedUnion]
public partial struct Command
{
    [Variant] public static partial Command Run(float speed);
    [Variant] public static partial Command Stop(string reason);
    [Variant] public static partial Command Pause();
}

public static class Consumer
{
    public static int Match(Command command)
    {
        switch (command)
        {
            case { kind: Command.Kind.Run, run: { speed: var speed } }:
                return (int)speed;
            case { IsStop: true, stop: var (reason) }:
                throw new System.NotImplementedException();
            case { IsPause: true }:
                throw new System.NotImplementedException();
        }

        return -1;
    }
}
