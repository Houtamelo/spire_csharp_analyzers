//@ exhaustive
// Declaration pattern is wildcard for same type
public class DeclarationPattern_Wildcard
{
    public int Test(bool b) => b switch
    {
        bool x => x ? 1 : 2,
    };
}
