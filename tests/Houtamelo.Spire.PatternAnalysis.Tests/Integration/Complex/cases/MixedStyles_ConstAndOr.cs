//@ exhaustive
// Mix of single constant patterns and or-pattern in one switch
public class MixedStyles_ConstAndOr
{
    public int Test(Color c) => c switch
    {
        Color.Red => 1,
        Color.Green or Color.Blue => 2,
        Color.Yellow => 3,
        Color.Purple => 4,
    };
}
