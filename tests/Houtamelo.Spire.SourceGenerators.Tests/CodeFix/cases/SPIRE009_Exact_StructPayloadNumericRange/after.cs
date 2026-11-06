using Houtamelo.Spire;

[DiscriminatedUnion]
partial struct Command
{
    [Variant] public static partial Command A(int value);
    [Variant] public static partial Command B();
}

class Consumer
{
    int Read(Command command) => command switch
    {
        { kind: Command.Kind.A, a: { value: < 0 } } => -1,
        { kind: Command.Kind.A, a: { value: > 10 } } => 1,
        { kind: Command.Kind.B } => 2,
        { IsA: true, a: { value: >= 0 and <= 10 } } => throw new System.NotImplementedException(),
    };
}
