//@ should_pass
// Ensure that SPIRE009 is NOT triggered when typed Moving and Speaking arms cover all variants.
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

static class Consumer
{
    static void Update(State state)
    {
        switch (state)
        {
            case { IsIdle: true }:
                break;
            case { IsMoving: true, moving: (Point destination, _) }:
                break;
            case { IsSpeaking: true, speaking: var (text, timer) }:
                break;
        }
    }
}
