//@ exhaustive
// Side + bool, using wildcard for one arm
public class EnumBoolWithWildcard
{
    public int Test(Side s, bool b) => (s, b) switch
    {
        (Side.Left, true) => 1,
        (Side.Left, false) => 2,
        (Side.Right, _) => 3,
    };
}
