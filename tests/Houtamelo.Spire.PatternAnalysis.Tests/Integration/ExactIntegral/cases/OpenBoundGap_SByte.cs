//@ not_exhaustive
// <= 0 and >= 2 leave exactly sbyte value 1 uncovered.
public class OpenBoundGap_SByte
{
    public int Test(sbyte value) => value switch
    {
        <= 0 => 0,
        >= 2 => 1,
        //~ 1
    };
}
