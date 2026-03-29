//@ exhaustive
// Sealed class covered by empty property pattern
#nullable enable

[EnforceExhaustiveness]
public sealed class Flag { }

public class Sealed_EmptyBraces
{
    public int Test(Flag f) => f switch
    {
        {} => 1,
    };
}
