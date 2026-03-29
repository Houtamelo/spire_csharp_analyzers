//@ exhaustive
// Sealed class covered by wildcard
#nullable enable

[EnforceExhaustiveness]
public sealed class Token { }

public class Sealed_Wildcard
{
    public int Test(Token t) => t switch
    {
        _ => 1,
    };
}
