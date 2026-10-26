//@ exhaustive
// The complete ulong domain is split at zero and one.
public class SignedWidths_ULong
{
    public int Test(ulong value) => value switch
    {
        0 => 0,
        > 0 => 1,
    };
}
