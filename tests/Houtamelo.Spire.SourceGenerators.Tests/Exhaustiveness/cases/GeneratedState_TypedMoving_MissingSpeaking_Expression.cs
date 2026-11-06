//@ should_fail
// Ensure that SPIRE009 IS triggered when a typed Moving payload arm omits Speaking in an expression.
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
    static int Read(State state) => state switch //~ ERROR
    {
        { IsIdle: true } => 0,
        { IsMoving: true, moving: (Point destination, _) } => 1,
    };
}
