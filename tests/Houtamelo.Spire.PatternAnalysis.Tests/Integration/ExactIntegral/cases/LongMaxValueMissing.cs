//@ not_exhaustive
// The upper signed endpoint is not rounded away.
public class LongMaxValueMissing
{
    public int Test(long value) => value switch
    {
        < long.MaxValue => 1,
        //~ long.MaxValue
    };
}
