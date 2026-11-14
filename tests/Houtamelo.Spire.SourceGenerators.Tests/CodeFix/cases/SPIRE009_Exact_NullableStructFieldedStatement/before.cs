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
        }
    }
}
