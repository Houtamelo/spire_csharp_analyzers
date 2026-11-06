//@ should_fail
// Ensure that SPIRE009 IS triggered when bool and nullable-reference payloads omit a correlated hole.
#nullable enable
using Houtamelo.Spire;

[DiscriminatedUnion]
partial struct Choice
{
    [Variant] public static partial Choice Pair(bool flag, string? text);
    [Variant] public static partial Choice Empty();
}

static class Consumer
{
    static int Read(Choice choice) => choice switch //~ ERROR
    {
        { IsPair: true, pair: (true, var _) } => 1,
        { IsPair: true, pair: (false, null) } => 2,
        { IsEmpty: true } => 0,
    };
}
