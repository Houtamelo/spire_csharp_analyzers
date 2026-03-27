//@ exhaustive
// Three-way partition: < 0, 0, > 0
public class Relational_ThreeWayPartition
{
    public int Test(int x) => x switch
    {
        < 0 => -1,
        0 => 0,
        > 0 => 1,
    };
}
