//@ exhaustive
// string? with null + not-null covers all
#nullable enable
public class NullableRefNotNull
{
    public int Test(string? s) => s switch
    {
        null => 0,
        not null => 1,
    };
}
