//@ not_exhaustive
// Or-patterns missing Purple
public class OrPattern_EnumMissing
{
    public int Test(Color c) => c switch
    {
        Color.Red or Color.Green => 1,
        Color.Blue or Color.Yellow => 2,
        //~ Color.Purple
    };
}
