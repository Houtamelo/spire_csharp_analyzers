//@ should_fail
// Ensure that SPIRE009 IS triggered when a typed Moving payload arm omits Speaking.
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
        switch (state) //~ ERROR
        {
            case { IsIdle: true }:
                break;
            case { IsMoving: true, moving: (Point destination, _) }:
                break;
        }
    }
}
