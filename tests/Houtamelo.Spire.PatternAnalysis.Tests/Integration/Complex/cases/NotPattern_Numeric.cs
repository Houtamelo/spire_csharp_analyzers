//@ exhaustive
// 0 and not 0 partition integers fully
public class NotPattern_Numeric
{
    public int Test(int x) => x switch
    {
        0 => 1,
        not 0 => 2,
    };
}
