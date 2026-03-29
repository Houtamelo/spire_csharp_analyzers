//@ exhaustive
// Sealed [EnforceExhaustiveness] type matched by type pattern
#nullable enable

[EnforceExhaustiveness]
public sealed class Stamp { }

public class Sealed_NotNull
{
    public int Test(Stamp s) => s switch
    {
        Stamp => 1,
    };
}
