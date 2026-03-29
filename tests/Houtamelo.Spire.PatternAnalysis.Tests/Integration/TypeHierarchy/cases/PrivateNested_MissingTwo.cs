//@ not_exhaustive
// Private-ctor base missing two of four nested subtypes
#nullable enable

[EnforceExhaustiveness]
public abstract class Suit2
{
    private Suit2() { }
    public sealed class Hearts : Suit2 { }
    public sealed class Diamonds : Suit2 { }
    public sealed class Clubs : Suit2 { }
    public sealed class Spades : Suit2 { }
}

public class PrivateNested_MissingTwo
{
    public int Test(Suit2 s) => s switch
    {
        Suit2.Hearts => 1,
        Suit2.Diamonds => 2,
        //~ Suit2.Clubs
        //~ Suit2.Spades
    };
}
