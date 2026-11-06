#nullable enable
using Houtamelo.Spire;

public readonly struct Point
{
}

[DiscriminatedUnion]
partial struct State
{
    [Variant] public static partial State Idle();
    [Variant] public static partial State Moving(Point destination, object? marker);
    [Variant] public static partial State Speaking(string text, float timer);
}

class Consumer
{
    static void Tick(State state)
    {
        switch (state)
        {
            case { IsIdle: true }:
                break;

            case { IsMoving: true, moving: (Point destination, _) }:
                break;

            case { IsSpeaking: true, speaking: var (text, timer) }:
                throw new System.NotImplementedException();
        }
    }
}
