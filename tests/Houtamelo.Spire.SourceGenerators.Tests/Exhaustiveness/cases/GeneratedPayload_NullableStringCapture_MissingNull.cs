//@ should_fail
// Ensure that SPIRE009 IS triggered when a typed string capture excludes null from a nullable payload.
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
        { IsText: true, text: (string value) } => value.Length,
        { IsEmpty: true } => 0,
    };
}
