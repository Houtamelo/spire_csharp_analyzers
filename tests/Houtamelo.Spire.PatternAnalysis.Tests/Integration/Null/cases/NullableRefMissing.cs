//@ not_exhaustive
// string? with only not-null — missing null
#nullable enable
public class NullableRefMissing
{
    public int Test(string? s) => s switch
    {
        not null => 1,
        //~ null
    };
}
