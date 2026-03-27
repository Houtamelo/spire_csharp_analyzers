//@ not_exhaustive
// Missing Blue
public class MissingOne
{
    public int Test(Color c) => c switch
    {
        Color.Red => 1,
        Color.Green => 2,
        //~ Color.Blue
    };
}
