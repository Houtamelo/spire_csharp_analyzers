//@ should_pass
// Readonly ordinary struct unions remain supported.
using Houtamelo.Spire;

[DiscriminatedUnion]
public readonly partial struct ImmutableUnion
{
    [Variant] public static partial ImmutableUnion Value(int amount);
}
