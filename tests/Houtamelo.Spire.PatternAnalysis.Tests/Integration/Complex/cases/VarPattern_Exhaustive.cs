//@ exhaustive
// var pattern matches everything
public class VarPattern_Exhaustive
{
    public int Test(int x) => x switch
    {
        var v => v,
    };
}
