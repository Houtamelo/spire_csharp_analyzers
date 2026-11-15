//@ should_pass
// Unannotated nested records do not affect a valid ordinary struct union.
using Houtamelo.Spire;

[DiscriminatedUnion]
public partial struct WorkingUnion
{
    [Variant] public static partial WorkingUnion Value(int amount);
    public record struct Helper(int amount);
}
