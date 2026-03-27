//@ exhaustive
// All enum members covered
public class AllMembers
{
    public int Test(Color c) => c switch
    {
        Color.Red => 1,
        Color.Green => 2,
        Color.Blue => 3,
    };
}
