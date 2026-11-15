//@ codefix: Spire.Union.RemoveRecord
using Houtamelo.Spire;

[DiscriminatedUnion]
public readonly partial record struct ImmutableState
{
    [Variant] public static partial ImmutableState Value(int amount);
}
