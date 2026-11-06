using Houtamelo.Spire;

[DiscriminatedUnion]
partial struct Command
{
    [Variant] public static partial Command A(float value);
    [Variant] public static partial Command B();
}

class Consumer
{
    int Read(Command command) => command switch
    {
        { kind: Command.Kind.A, a: { value: < 0f } } => -1,
        { kind: Command.Kind.A, a: { value: > 10f } } => 1,
        { kind: Command.Kind.B } => 2,
        { IsA: true, a: { value: >= 0f and <= 10f } } => throw new System.NotImplementedException(),
    };
}
