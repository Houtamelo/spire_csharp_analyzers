//@ should_fail
// Ensure that SPIRE009 IS triggered when correlated bool payloads omit one combination.
#nullable enable
using Houtamelo.Spire;

[DiscriminatedUnion]
partial struct Choice
{
    [Variant] public static partial Choice Pair(bool first, bool second);
    [Variant] public static partial Choice Empty();
}

static class Consumer
{
    static int Read(Choice choice) => choice switch //~ ERROR
    {
        { IsPair: true, pair: (true, true) } => 1,
        { IsPair: true, pair: (true, false) } => 2,
        { IsPair: true, pair: (false, true) } => 3,
        { IsEmpty: true } => 0,
    };
}
