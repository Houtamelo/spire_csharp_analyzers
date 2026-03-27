//@ exhaustive
// All 5 Color members listed individually
public class LargeEnum_AllFive
{
    public int Test(Color c) => c switch
    {
        Color.Red => 1,
        Color.Green => 2,
        Color.Blue => 3,
        Color.Yellow => 4,
        Color.Purple => 5,
    };
}
