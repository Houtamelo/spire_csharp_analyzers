//@ exhaustive
// Overlapping ranges that together cover everything
public class Relational_OverlappingRanges
{
    public int Test(int x) => x switch
    {
        <= 50 => 1,
        > 0 => 2,
    };
}
