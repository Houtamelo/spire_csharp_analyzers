//@ not_exhaustive
// Statement patterns use the same exact interval conversion as expressions.
public class LongStatementGap
{
    public int Test(long value)
    {
        switch (value)
        {
            case <= 9007199254740992L:
                return 0;
            case >= 9007199254740994L:
                return 1;
            //~ 9007199254740993L
        }

        return -1;
    }
}
