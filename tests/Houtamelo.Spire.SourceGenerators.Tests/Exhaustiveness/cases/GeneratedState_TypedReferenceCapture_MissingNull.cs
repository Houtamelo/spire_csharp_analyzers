//@ should_fail
// Ensure that SPIRE009 IS triggered when a same-type string capture excludes null from a nullable payload.
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
    [Variant] public static partial State Speaking(string? text, float timer);
}

static class Consumer
{
    static void Update(State state)
    {
        switch (state) //~ ERROR
        {
            case { IsIdle: true }:
                break;
            case { IsMoving: true, moving: (Point destination, _) }:
                break;
            case { IsSpeaking: true, speaking: (string text, var timer) }:
                break;
        }
    }
}
