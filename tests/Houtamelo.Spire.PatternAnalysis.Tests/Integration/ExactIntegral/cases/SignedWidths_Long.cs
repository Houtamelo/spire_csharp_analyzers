//@ exhaustive
// The complete long domain is split at zero.
public class SignedWidths_Long
{
    public int Test(long value) => value switch
    {
        < 0 => -1,
        0 => 0,
        > 0 => 1,
    };
}
