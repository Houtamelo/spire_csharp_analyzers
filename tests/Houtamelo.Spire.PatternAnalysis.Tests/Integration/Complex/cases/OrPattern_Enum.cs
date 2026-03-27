//@ exhaustive
// Or-patterns grouping enum members into two arms covers all
public class OrPattern_Enum
{
    public int Test(Color c) => c switch
    {
        Color.Red or Color.Green => 1,
        Color.Blue or Color.Yellow or Color.Purple => 2,
    };
}
