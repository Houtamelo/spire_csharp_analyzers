//@ not_exhaustive
// Missing Yellow and Purple
public class LargeEnum_MissingTwo
{
    public int Test(Color c) => c switch
    {
        Color.Red => 1,
        Color.Green => 2,
        Color.Blue => 3,
        //~ Color.Yellow
        //~ Color.Purple
    };
}
