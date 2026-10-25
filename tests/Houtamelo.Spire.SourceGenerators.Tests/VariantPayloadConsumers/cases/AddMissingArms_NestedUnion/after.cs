using Houtamelo.Spire;

public static partial class Outer
{
    [DiscriminatedUnion]
    public partial struct Command
    {
        [Variant] public static partial Command Run(float speed);
        [Variant] public static partial Command Stop();
    }
}

public static class Consumer
{
    public static float Match(Outer.Command command) => command switch
    {
        { kind: Outer.Command.Kind.Run, run: { speed: var speed } } => speed,
        { kind: Outer.Command.Kind.Stop } => throw new System.NotImplementedException()
    };
}
