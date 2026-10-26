//@ not_exhaustive
// <= 0 and >= 2 leave exactly short value 1 uncovered.
public class OpenBoundGap_Short
{
    public int Test(short value) => value switch
    {
        <= 0 => 0,
        >= 2 => 1,
        //~ 1
    };
}
