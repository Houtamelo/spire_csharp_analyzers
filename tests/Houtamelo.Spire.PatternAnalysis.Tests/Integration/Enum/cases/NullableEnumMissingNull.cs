//@ not_exhaustive
// Color? missing null case
public class NullableEnumMissingNull
{
    public int Test(Color? c) => c switch
    {
        Color.Red => 1,
        Color.Green => 2,
        Color.Blue => 3,
    };
}
