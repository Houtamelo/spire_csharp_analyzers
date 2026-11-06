//@ should_fail
// Ensure that SPIRE009 IS triggered when a generated bool payload covers only true.
#nullable enable
using Houtamelo.Spire;

[DiscriminatedUnion]
partial struct Choice
{
    [Variant] public static partial Choice Flag(bool value);
    [Variant] public static partial Choice Empty();
}

static class Consumer
{
    static int Read(Choice choice) => choice switch //~ ERROR
    {
        { IsFlag: true, flag: (true) } => 1,
        { IsEmpty: true } => 0,
    };
}
