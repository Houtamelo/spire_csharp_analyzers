//@ not_exhaustive
// <= 0 and >= 2 leave exactly ulong value 1 uncovered.
public class OpenBoundGap_ULong
{
    public int Test(ulong value) => value switch
    {
        <= 0 => 0,
        >= 2 => 1,
        //~ 1UL
    };
}
