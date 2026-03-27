//@ not_exhaustive
// not Red covers Green/Blue/Yellow/Purple but Red itself is missing
public class NotPattern_EnumMissing
{
    public int Test(Color c) => c switch
    {
        not Color.Red => 1,
        //~ Color.Red
    };
}
