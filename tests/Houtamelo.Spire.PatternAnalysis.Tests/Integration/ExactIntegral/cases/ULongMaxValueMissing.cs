//@ not_exhaustive
// ulong.MaxValue remains matchable despite exceeding signed and double ranges.
public class ULongMaxValueMissing
{
    public int Test(ulong value) => value switch
    {
        < ulong.MaxValue => 1,
        //~ ulong.MaxValue
    };
}
