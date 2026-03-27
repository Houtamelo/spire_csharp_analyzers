//@ not_exhaustive
// (bool?, bool) — missing null case
public class NullableTupleElementMissing
{
    public int Test(bool? a, bool b) => (a, b) switch
    {
        (true, true) => 1,
        (true, false) => 2,
        (false, _) => 3,
        //~ (null, _)
    };
}
