//@ not_exhaustive
// <= 0 and >= 2 leave exactly int value 1 uncovered.
public class OpenBoundGap_Int
{
    public int Test(int value) => value switch
    {
        <= 0 => 0,
        >= 2 => 1,
        //~ 1
    };
}
