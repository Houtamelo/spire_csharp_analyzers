//@ not_exhaustive
// The lower signed endpoint is not rounded away.
public class LongMinValueMissing
{
    public int Test(long value) => value switch
    {
        > long.MinValue => 1,
        //~ long.MinValue
    };
}
