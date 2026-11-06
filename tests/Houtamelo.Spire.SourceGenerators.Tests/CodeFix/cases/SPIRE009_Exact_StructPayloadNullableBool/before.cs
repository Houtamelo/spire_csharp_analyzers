#nullable enable
using Houtamelo.Spire;

[DiscriminatedUnion]
partial struct Command
{
    [Variant] public static partial Command A(bool? flag);
    [Variant] public static partial Command B();
}

class Consumer
{
    int Read(Command command) => command switch
    {
        { kind: Command.Kind.A, a: { flag: true } } => 1,
        { kind: Command.Kind.A, a: { flag: false } } => 2,
        { kind: Command.Kind.B } => 3,
    };
}
