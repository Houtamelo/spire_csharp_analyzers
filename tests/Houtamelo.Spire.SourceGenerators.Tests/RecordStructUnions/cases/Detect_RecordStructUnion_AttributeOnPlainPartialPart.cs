//@ should_fail
// The union attribute may be on a plain partial part while another part uses record struct.
using Houtamelo.Spire;

[DiscriminatedUnion]
public partial struct SplitUnion
{
    [Variant] public static partial SplitUnion Value(int amount);
}

public partial record struct SplitUnion //~ ERROR
{
}
