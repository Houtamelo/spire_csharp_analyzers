//@ exhaustive
// All 5 Color members covered via two or-groups
public class LargeEnum_OrGroups
{
    public int Test(Color c) => c switch
    {
        Color.Red or Color.Green or Color.Blue => 1,
        Color.Yellow or Color.Purple => 2,
    };
}
