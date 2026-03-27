//@ exhaustive
// Or-combination: <= -1 or >= 1 combined with 0
public class Relational_OrCombination
{
    public int Test(int x) => x switch
    {
        <= -1 or >= 1 => 1,
        0 => 0,
    };
}
