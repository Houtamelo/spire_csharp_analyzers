//@ should_fail
// A nested struct-based union cannot use the record keyword either.
using Houtamelo.Spire;

public static class Host
{
    [DiscriminatedUnion]
    public partial record struct NestedUnion //~ ERROR
    {
        [Variant] public static partial NestedUnion Value(int amount);
    }
}
