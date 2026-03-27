//@ exhaustive
// One specific arm plus wildcard catchall covers all
public class MixedStyles_WildcardCatchall
{
    public int Test(Color c) => c switch
    {
        Color.Red => 1,
        _ => 2,
    };
}
