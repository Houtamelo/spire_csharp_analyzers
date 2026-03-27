//@ exhaustive
// Empty property pattern {} acts as wildcard
public class EmptyPropertyPattern
{
    public int Test(SingleField s) => s switch
    {
        {} => 1,
    };
}
