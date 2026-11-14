#nullable enable
using Houtamelo.Spire;

[DiscriminatedUnion]
partial struct Command
{
    [Variant] public static partial Command Idle();
    [Variant] public static partial Command Move(int distance, string? marker);
    [Variant] public static partial Command Enabled(bool enabled);
}

class Consumer
{
    static void Read(Command? command)
    {
        switch (command)
        {
            case { IsIdle: true }:
                throw new System.NotImplementedException();
            case { IsMove: true, move: var (distance, marker) }:
                throw new System.NotImplementedException();
            case { IsEnabled: true, enabled: var (enabled) }:
                throw new System.NotImplementedException();
            case null:
                throw new System.NotImplementedException();
        }
    }
}
