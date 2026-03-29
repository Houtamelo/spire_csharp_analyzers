//@ not_exhaustive
// Private-ctor base missing one nested subtype
#nullable enable

[EnforceExhaustiveness]
public abstract class Token2
{
    private Token2() { }
    public sealed class Keyword : Token2 { }
    public sealed class Identifier : Token2 { }
    public sealed class Number : Token2 { }
}

public class PrivateNested_MissingOne
{
    public int Test(Token2 t) => t switch
    {
        Token2.Keyword => 1,
        Token2.Identifier => 2,
        //~ Token2.Number
    };
}
