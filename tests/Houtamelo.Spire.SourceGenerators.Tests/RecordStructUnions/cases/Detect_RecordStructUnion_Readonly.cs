//@ should_fail
// Readonly is allowed for struct unions, but record is not.
using Houtamelo.Spire;

[DiscriminatedUnion]
public readonly partial record struct ImmutableUnion //~ ERROR
{
    [Variant] public static partial ImmutableUnion Value(int amount);
}
