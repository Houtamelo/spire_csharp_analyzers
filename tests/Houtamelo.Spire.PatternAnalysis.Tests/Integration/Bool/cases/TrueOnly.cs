//@ not_exhaustive
// Missing false
public class TrueOnly
{
    public int Test(bool b) => b switch
    {
        true => 1,
    };
}
