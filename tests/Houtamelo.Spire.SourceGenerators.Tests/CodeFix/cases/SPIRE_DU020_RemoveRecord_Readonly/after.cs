//@ codefix: Spire.Union.RemoveRecord
using Houtamelo.Spire;

[DiscriminatedUnion]
public readonly partial struct ImmutableState
{
    [Variant] public static partial ImmutableState Value(int amount);
}
