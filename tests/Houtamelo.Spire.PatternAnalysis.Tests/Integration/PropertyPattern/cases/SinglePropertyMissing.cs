//@ not_exhaustive
// Only { Flag: true } — missing false
public class SinglePropertyMissing
{
    public int Test(Pair p) => p switch
    {
        { Flag: true } => 1,
        //~ { Flag: false }
    };
}
