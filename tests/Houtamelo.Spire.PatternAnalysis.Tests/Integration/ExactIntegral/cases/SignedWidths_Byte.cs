//@ exhaustive
// The complete byte domain is split at zero and one.
public class SignedWidths_Byte
{
    public int Test(byte value) => value switch
    {
        0 => 0,
        > 0 => 1,
    };
}
