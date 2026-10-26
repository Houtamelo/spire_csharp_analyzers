//@ not_exhaustive
// A typed const identifier follows the same exact path as a suffixed literal.
public class LongConstIdentifierGap
{
    const long Missing = 9007199254740993L;

    public int Test(long value) => value switch
    {
        < Missing => 0,
        > Missing => 1,
        //~ Missing
    };
}
