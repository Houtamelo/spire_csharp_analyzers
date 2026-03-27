//@ not_exhaustive
// int? with > 0 and <= 0 but no null
public class Nullable_NumericMissingNull
{
    public int Test(int? x) => x switch
    {
        > 0 => 1,
        <= 0 => 2,
    };
}
