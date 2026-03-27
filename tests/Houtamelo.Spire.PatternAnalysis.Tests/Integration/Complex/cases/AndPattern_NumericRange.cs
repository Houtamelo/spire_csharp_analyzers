//@ exhaustive
// Three and-pattern arms partition integers fully
public class AndPattern_NumericRange
{
    public int Test(int x) => x switch
    {
        > 0 and <= 100 => 1,
        > 100 => 2,
        <= 0 => 3,
    };
}
