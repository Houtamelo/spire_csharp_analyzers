//@ should_pass
// Ensure that SPIRE009 is NOT triggered when mixed positional and property enum partitions are complete.
#nullable enable
using Houtamelo.Spire;

enum Color
{
    Red,
    Green,
    Blue,
}

[DiscriminatedUnion]
partial struct Choice
{
    [Variant] public static partial Choice Pair(Color color, bool enabled);
    [Variant] public static partial Choice Empty();
}

static class Consumer
{
    static int Read(Choice choice) => choice switch
    {
        { IsPair: true, pair: (Color.Red, var _) } => 1,
        { IsPair: true, pair: { color: Color.Green } } => 2,
        { IsPair: true, pair: { color: Color.Blue, enabled: true } } => 3,
        { IsPair: true, pair: { color: Color.Blue, enabled: false } } => 4,
        { IsEmpty: true } => 0,
    };
}
