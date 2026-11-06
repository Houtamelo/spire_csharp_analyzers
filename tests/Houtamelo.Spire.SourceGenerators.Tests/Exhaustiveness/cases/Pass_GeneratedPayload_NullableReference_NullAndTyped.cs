//@ should_pass
// Ensure that SPIRE009 is NOT triggered when generated nullable references cover null and typed non-null values.
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
    static int Read(Choice choice) => choice switch
    {
        { IsText: true, text: (null) } => 1,
        { IsText: true, text: (string value) } => value.Length,
        { IsEmpty: true } => 0,
    };
}
