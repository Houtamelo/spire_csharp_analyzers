#nullable enable
using Houtamelo.Spire;

[DiscriminatedUnion]
partial struct Command
{
    [Variant] public static partial Command @null(int value);
    [Variant] public static partial Command Other();
}

class Consumer
{
    int Read(Command? command) => command switch
    {
        { kind: Command.Kind.Other } => 0,
    };
}
