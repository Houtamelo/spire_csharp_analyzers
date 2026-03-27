//@ exhaustive
// ((bool, bool), bool) — nested tuple
public class NestedTuple_2x2
{
    public int Test(bool a, bool b, bool c) => ((a, b), c) switch
    {
        ((true, _), _) => 1,
        ((false, true), _) => 2,
        ((false, false), true) => 3,
        ((false, false), false) => 4,
    };
}
