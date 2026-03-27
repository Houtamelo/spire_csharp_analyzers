//@ not_exhaustive
// Missing (false, false)
public class BoolBoolMissing
{
    public int Test(bool a, bool b) => (a, b) switch
    {
        (true, true) => 1,
        (true, false) => 2,
        (false, true) => 3,
        //~ (false, false)
    };
}
