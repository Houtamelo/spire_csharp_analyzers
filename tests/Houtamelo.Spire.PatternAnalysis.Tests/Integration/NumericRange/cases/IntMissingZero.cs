//@ not_exhaustive
// > 0 and < 0 misses zero
public class IntMissingZero
{
    public int Test(int x) => x switch
    {
        > 0 => 1,
        < 0 => 2,
        //~ 0
    };
}
