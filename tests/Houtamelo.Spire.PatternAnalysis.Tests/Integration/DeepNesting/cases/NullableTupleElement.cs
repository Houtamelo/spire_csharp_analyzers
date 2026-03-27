//@ exhaustive
// (bool?, bool) — nullable in first tuple position
public class NullableTupleElement
{
    public int Test(bool? a, bool b) => (a, b) switch
    {
        (null, _) => 0,
        (true, true) => 1,
        (true, false) => 2,
        (false, _) => 3,
    };
}
