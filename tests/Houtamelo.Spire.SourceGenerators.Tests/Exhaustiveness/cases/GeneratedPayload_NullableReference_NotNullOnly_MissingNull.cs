//@ should_fail
// Ensure that SPIRE009 IS triggered when a generated nullable reference payload covers only non-null values.
#nullable enable
using Houtamelo.Spire;

[DiscriminatedUnion]
partial struct Choice
{
    [Variant] public static partial Choice Text(string? value);
    [Variant] public static partial Choice Empty();
}

static class Consumer
{
    static int Read(Choice choice) => choice switch //~ ERROR
    {
        { IsText: true, text: (not null) } => 1,
        { IsEmpty: true } => 0,
    };
}
