//@ exhaustive
// (((bool, bool), bool), bool) — 3 levels deep
public class TripleNested
{
    public int Test(bool a, bool b, bool c, bool d) => (((a, b), c), d) switch
    {
        (((true, _), _), _) => 1,
        (((false, _), _), _) => 2,
    };
}
