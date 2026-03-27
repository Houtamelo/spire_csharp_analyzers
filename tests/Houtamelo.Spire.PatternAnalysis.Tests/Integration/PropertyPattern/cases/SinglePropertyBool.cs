//@ exhaustive
// { Flag: true } + { Flag: false } covers all
public class SinglePropertyBool
{
    public int Test(Pair p) => p switch
    {
        { Flag: true } => 1,
        { Flag: false } => 2,
    };
}
