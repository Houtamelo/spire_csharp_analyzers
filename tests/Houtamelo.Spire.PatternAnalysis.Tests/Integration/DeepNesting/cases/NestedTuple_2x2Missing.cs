//@ not_exhaustive
// ((bool, bool), bool) — missing (false, false, false)
public class NestedTuple_2x2Missing
{
    public int Test(bool a, bool b, bool c) => ((a, b), c) switch
    {
        ((true, _), _) => 1,
        ((false, true), _) => 2,
        ((false, false), true) => 3,
    };
}
