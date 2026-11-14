#nullable enable
using Houtamelo.Spire;

[DiscriminatedUnion]
partial struct Command
{
    [Variant] public static partial Command Move(int distance, string? marker);
    [Variant] public static partial Command Idle();
}

class Consumer
{
    static void Read(Command? command)
    {
        switch (command)
        {
            case null:
                break;
            case { IsMove: true, move: var (distance, marker) }:
                throw new System.NotImplementedException();
            case { IsIdle: true }:
                throw new System.NotImplementedException();
        }
    }
}
