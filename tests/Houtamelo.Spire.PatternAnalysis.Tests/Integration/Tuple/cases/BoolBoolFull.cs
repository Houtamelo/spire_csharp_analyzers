//@ exhaustive
// All 4 combinations of (bool, bool)
public class BoolBoolFull
{
    public int Test(bool a, bool b) => (a, b) switch
    {
        (true, true) => 1,
        (true, false) => 2,
        (false, true) => 3,
        (false, false) => 4,
    };
}
