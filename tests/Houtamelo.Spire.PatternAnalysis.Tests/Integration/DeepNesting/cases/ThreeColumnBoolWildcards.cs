//@ exhaustive
// (bool, bool, bool) covered with wildcards
public class ThreeColumnBoolWildcards
{
    public int Test(bool a, bool b, bool c) => (a, b, c) switch
    {
        (true, _, _) => 1,
        (false, true, _) => 2,
        (false, false, _) => 3,
    };
}
