//@ not_exhaustive
// <= 0 and >= 2 leave exactly ushort value 1 uncovered.
public class OpenBoundGap_UShort
{
    public int Test(ushort value) => value switch
    {
        <= 0 => 0,
        >= 2 => 1,
        //~ 1
    };
}
