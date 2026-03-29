//@ exhaustive
// Sealed class — only needs a single type match
#nullable enable

[EnforceExhaustiveness]
public sealed class Singleton { }

public class Sealed_ClassOnly
{
    public int Test(Singleton s) => s switch
    {
        Singleton => 1,
    };
}
