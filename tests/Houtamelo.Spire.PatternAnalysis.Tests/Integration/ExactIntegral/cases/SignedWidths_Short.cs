//@ exhaustive
// The complete short domain is split at zero.
public class SignedWidths_Short
{
    public int Test(short value) => value switch
    {
        < 0 => -1,
        0 => 0,
        > 0 => 1,
    };
}
