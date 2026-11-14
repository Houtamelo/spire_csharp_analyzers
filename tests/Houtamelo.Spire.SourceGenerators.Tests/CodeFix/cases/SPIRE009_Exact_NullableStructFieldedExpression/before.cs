#nullable enable
using Houtamelo.Spire;

[DiscriminatedUnion]
partial struct Command
{
    [Variant] public static partial Command Idle();
    [Variant] public static partial Command Move(int distance, string? marker);
    [Variant] public static partial Command Enabled(bool enabled, string? note);
}

class Consumer
{
    int Read(Command? command) => command switch
    {
        { kind: Command.Kind.Idle } => 0,
    };
}
