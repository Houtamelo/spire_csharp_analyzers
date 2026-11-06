//@ should_pass
// Ensure that SPIRE009 is NOT triggered when generated bool payloads cover both values.
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
    static int Read(Choice choice) => choice switch
    {
        { IsFlag: true, flag: (true) } => 1,
        { IsFlag: true, flag: (false) } => 2,
        { IsEmpty: true } => 0,
    };
}
