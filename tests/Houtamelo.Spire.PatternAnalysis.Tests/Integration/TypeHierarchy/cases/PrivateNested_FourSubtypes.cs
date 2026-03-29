//@ exhaustive
// Private-ctor base with four nested subtypes
#nullable enable

[EnforceExhaustiveness]
public abstract class Direction2
{
    private Direction2() { }
    public sealed class North : Direction2 { }
    public sealed class South : Direction2 { }
    public sealed class East : Direction2 { }
    public sealed class West : Direction2 { }
}

public class PrivateNested_FourSubtypes
{
    public int Test(Direction2 d) => d switch
    {
        Direction2.North => 1,
        Direction2.South => 2,
        Direction2.East => 3,
        Direction2.West => 4,
    };
}
