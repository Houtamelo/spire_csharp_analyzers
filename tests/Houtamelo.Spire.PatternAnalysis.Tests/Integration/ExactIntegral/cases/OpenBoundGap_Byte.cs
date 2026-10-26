//@ not_exhaustive
// <= 0 and >= 2 leave exactly byte value 1 uncovered.
public class OpenBoundGap_Byte
{
    public int Test(byte value) => value switch
    {
        <= 0 => 0,
        >= 2 => 1,
        //~ 1
    };
}
