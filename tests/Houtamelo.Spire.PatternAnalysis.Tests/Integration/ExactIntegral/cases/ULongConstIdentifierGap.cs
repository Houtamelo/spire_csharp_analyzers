//@ not_exhaustive
// A typed ulong const identifier must not be narrowed through a signed conversion.
public class ULongConstIdentifierGap
{
    const ulong Missing = 9007199254740993UL;

    public int Test(ulong value) => value switch
    {
        < Missing => 0,
        > Missing => 1,
        //~ Missing
    };
}
