//@ exhaustive
// Both bool values covered
public class TrueAndFalse
{
    public int Test(bool b) => b switch
    {
        true => 1,
        false => 2,
    };
}
