//@ exhaustive
// The complete ushort domain is split at zero and one.
public class SignedWidths_UShort
{
    public int Test(ushort value) => value switch
    {
        0 => 0,
        > 0 => 1,
    };
}
