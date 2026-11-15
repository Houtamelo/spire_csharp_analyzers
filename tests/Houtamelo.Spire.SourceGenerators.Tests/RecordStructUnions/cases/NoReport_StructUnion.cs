//@ should_pass
// Ordinary mutable struct unions remain supported.
using Houtamelo.Spire;

[DiscriminatedUnion]
public partial struct WorkingUnion
{
    [Variant] public static partial WorkingUnion Value(int amount);
}
