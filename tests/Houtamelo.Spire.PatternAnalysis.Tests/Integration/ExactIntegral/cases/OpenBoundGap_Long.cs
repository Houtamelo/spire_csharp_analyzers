//@ not_exhaustive
// <= 0 and >= 2 leave exactly long value 1 uncovered.
public class OpenBoundGap_Long
{
    public int Test(long value) => value switch
    {
        <= 0 => 0,
        >= 2 => 1,
        //~ 1L
    };
}
