//@ exhaustive
// > 50.0 and <= 50.0 covers all doubles (NaN excluded from universe)
public class DoubleRelational
{
    public int Test(double x) => x switch
    {
        > 50.0 => 1,
        <= 50.0 => 2,
    };
}
