//@ exhaustive
// Sealed class covered by not null pattern
#nullable enable

[EnforceExhaustiveness]
public sealed class Stamp { }

public class Sealed_NotNull
{
    public int Test(Stamp s) => s switch
    {
        not null => 1,
    };
}
