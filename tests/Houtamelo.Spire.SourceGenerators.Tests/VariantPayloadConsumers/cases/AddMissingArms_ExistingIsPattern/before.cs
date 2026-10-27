using Houtamelo.Spire;

[DiscriminatedUnion]
public partial struct Command
{
    [Variant] public static partial Command Run(float speed);
    [Variant] public static partial Command Stop();
    [Variant] public static partial Command Pause();
}

public static class Consumer
{
    public static int Match(Command command) => command switch
    {
        { kind: Command.Kind.Run, run: { speed: var speed } } => (int)speed,
        { IsStop: true } => 0,
    };
}
