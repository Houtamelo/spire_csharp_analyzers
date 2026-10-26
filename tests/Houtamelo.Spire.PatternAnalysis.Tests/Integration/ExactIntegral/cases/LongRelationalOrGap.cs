//@ not_exhaustive
// An or-pattern keeps both exact sides while leaving the middle interval open.
public class LongRelationalOrGap
{
    const long Lower = 9007199254740992L;
    const long Upper = 9007199254740994L;

    public int Test(long value) => value switch
    {
        <= Lower or >= Upper => 0,
        //~ > Lower and < Upper
    };
}
