//@ not_exhaustive
// Null is covered separately while the exact integral gap remains.
public class LongNullableGap
{
    const long Lower = 9007199254740992L;
    const long Upper = 9007199254740994L;

    public int Test(long? value) => value switch
    {
        null => 0,
        <= Lower => 1,
        >= Upper => 2,
        //~ 9007199254740993L
    };
}
