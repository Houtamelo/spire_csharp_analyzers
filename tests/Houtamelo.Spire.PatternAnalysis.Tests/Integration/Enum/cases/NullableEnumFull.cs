//@ exhaustive
// Color? with null + all members
public class NullableEnumFull
{
    public int Test(Color? c) => c switch
    {
        null => 0,
        Color.Red => 1,
        Color.Green => 2,
        Color.Blue => 3,
    };
}
