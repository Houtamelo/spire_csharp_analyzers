using Houtamelo.Spire;

[DiscriminatedUnion]
partial struct Command
{
    [Variant] public static partial Command A(bool flag);
    [Variant] public static partial Command B(bool flag);
}

class Consumer
{
    int Read(Command command) => command switch
    {
        { kind: Command.Kind.A, a: { flag: true } } => 1,
        { kind: Command.Kind.B, b: { flag: false } } => 2,
        { IsA: true, a: { flag: false } } => throw new System.NotImplementedException(),
        { IsB: true, b: { flag: true } } => throw new System.NotImplementedException(),
    };
}
