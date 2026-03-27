//@ exhaustive
// > 0 and <= 0 covers all integers
public class IntFullPartition
{
    public int Test(int x) => x switch
    {
        > 0 => 1,
        <= 0 => 2,
    };
}
