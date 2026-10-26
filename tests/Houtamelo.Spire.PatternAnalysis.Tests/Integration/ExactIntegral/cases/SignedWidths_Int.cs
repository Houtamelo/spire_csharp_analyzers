//@ exhaustive
// The complete int domain is split at zero.
public class SignedWidths_Int
{
    public int Test(int value) => value switch
    {
        < 0 => -1,
        0 => 0,
        > 0 => 1,
    };
}
