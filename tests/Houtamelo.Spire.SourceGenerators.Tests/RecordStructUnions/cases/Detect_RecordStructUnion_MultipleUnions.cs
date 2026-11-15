//@ should_fail
// An invalid record-struct union is diagnosed while an ordinary sibling remains valid.
using Houtamelo.Spire;

[DiscriminatedUnion]
public partial record struct BrokenUnion //~ ERROR
{
    [Variant] public record struct Value(int amount);
}

[DiscriminatedUnion]
public partial struct WorkingUnion
{
    [Variant] public static partial WorkingUnion Value(int amount);
}
