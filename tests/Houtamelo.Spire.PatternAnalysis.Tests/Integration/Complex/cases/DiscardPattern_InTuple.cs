//@ exhaustive
// Discard in second tuple position covers all; first partitioned by bool
public class DiscardPattern_InTuple
{
    public int Test(bool a, bool b) => (a, b) switch
    {
        (true, _) => 1,
        (false, _) => 2,
    };
}
