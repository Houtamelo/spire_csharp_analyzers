//@ exhaustive
// Single arm with or-pattern covers both bool values
public class OrPattern_Bool
{
    public int Test(bool b) => b switch
    {
        true or false => 1,
    };
}
