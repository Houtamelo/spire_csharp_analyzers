//@ exhaustive
// Discard pattern after explicit values is still exhaustive
public class RedundantArms
{
    public int Test(bool b) => b switch
    {
        true => 1,
        _ => 2,
    };
}
