//@ exhaustive
// The complete uint domain is split at zero and one.
public class SignedWidths_UInt
{
    public int Test(uint value) => value switch
    {
        0 => 0,
        > 0 => 1,
    };
}
