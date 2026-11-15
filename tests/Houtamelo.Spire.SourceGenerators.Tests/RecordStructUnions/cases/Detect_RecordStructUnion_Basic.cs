//@ should_fail
// A struct-based union cannot use the record keyword.
using Houtamelo.Spire;

[DiscriminatedUnion]
public partial record struct BrokenUnion //~ ERROR
{
    [Variant] public static partial BrokenUnion Value(int amount);
}
