//@ not_exhaustive
// <= 0 and >= 2 leave exactly uint value 1 uncovered.
public class OpenBoundGap_UInt
{
    public int Test(uint value) => value switch
    {
        <= 0 => 0,
        >= 2 => 1,
        //~ 1
    };
}
