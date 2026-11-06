using Houtamelo.Spire;

[DiscriminatedUnion]
partial struct Command
{
    [Variant] public static partial Command A(ulong value);
    [Variant] public static partial Command B();
}

class Consumer
{
    int Read(Command command) => command switch
    {
        { kind: Command.Kind.A, a: { value: < 9007199254740993UL } } => -1,
        { kind: Command.Kind.A, a: { value: > 9007199254740993UL } } => 1,
        { kind: Command.Kind.B } => 2,
    };
}
