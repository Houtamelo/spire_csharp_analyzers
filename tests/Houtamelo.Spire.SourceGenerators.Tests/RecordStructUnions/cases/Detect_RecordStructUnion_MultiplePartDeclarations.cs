//@ should_fail
// Multiple authored parts still describe one invalid record-struct union.
using Houtamelo.Spire;

[DiscriminatedUnion]
public partial record struct SplitUnion //~ ERROR
{
    [Variant] public static partial SplitUnion Value(int amount);
}

public partial record struct SplitUnion
{
}
