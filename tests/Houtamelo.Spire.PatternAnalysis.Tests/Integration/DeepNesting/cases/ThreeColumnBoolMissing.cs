//@ not_exhaustive
// (bool, bool, bool) missing (false, false, false)
public class ThreeColumnBoolMissing
{
    public int Test(bool a, bool b, bool c) => (a, b, c) switch
    {
        (true, true, true) => 1,
        (true, true, false) => 2,
        (true, false, true) => 3,
        (true, false, false) => 4,
        (false, true, true) => 5,
        (false, true, false) => 6,
        (false, false, true) => 7,
        //~ (false, false, false)
    };
}
