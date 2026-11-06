//@ should_fail
// Ensure that SPIRE009 IS triggered when a positional enum payload omits enum members.
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
    [Variant] public static partial Choice Color(Color value);
    [Variant] public static partial Choice Empty();
}

static class Consumer
{
    static int Read(Choice choice) => choice switch //~ ERROR
    {
        { IsColor: true, color: (Color.Red) } => 1,
        { IsEmpty: true } => 0,
    };
}
