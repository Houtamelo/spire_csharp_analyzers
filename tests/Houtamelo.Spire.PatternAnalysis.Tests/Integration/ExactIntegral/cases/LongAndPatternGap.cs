//@ not_exhaustive
// An and-pattern must preserve the single value between two exact bounds.
public class LongAndPatternGap
{
    public int Test(long value) => value switch
    {
        <= 9007199254740992L => 0,
        >= 9007199254740994L => 1,
        //~ > 9007199254740992L and < 9007199254740994L
    };
}
