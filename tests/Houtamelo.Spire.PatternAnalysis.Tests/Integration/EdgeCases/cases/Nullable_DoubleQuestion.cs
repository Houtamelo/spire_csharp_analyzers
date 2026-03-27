//@ exhaustive
// int? with null, > 0, <= 0
public class Nullable_DoubleQuestion
{
    public int Test(int? x) => x switch
    {
        null => 0,
        > 0 => 1,
        <= 0 => 2,
    };
}
