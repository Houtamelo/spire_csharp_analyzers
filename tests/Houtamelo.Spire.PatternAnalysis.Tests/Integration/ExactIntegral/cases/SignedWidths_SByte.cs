//@ exhaustive
// The complete sbyte domain is split at zero.
public class SignedWidths_SByte
{
    public int Test(sbyte value) => value switch
    {
        < 0 => -1,
        0 => 0,
        > 0 => 1,
    };
}
