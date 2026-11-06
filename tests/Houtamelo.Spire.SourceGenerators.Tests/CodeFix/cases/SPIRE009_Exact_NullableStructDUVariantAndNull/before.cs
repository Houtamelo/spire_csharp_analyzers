#nullable enable
using Houtamelo.Spire;

[DiscriminatedUnion]
partial struct Command
{
    [Variant] public static partial Command A();
    [Variant] public static partial Command B();
}

class Consumer
{
    int Read(Command? command) => command switch
    {
        { kind: Command.Kind.A } => 1,
    };
}
