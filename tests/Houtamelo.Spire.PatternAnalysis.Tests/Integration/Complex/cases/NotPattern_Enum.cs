//@ exhaustive
// Constant for Red plus not-Red covers all enum members
public class NotPattern_Enum
{
    public int Test(Color c) => c switch
    {
        Color.Red => 1,
        not Color.Red => 2,
    };
}
