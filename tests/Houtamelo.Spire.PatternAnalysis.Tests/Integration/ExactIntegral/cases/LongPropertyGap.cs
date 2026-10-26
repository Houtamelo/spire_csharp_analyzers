//@ not_exhaustive
// Property-pattern conversion retains the exact value in the nested numeric column.
public class LongPropertyGap
{
    const long Lower = 9007199254740992L;
    const long Upper = 9007199254740994L;

    public int Test(LongBox value) => value switch
    {
        { Value: <= Lower } => 0,
        { Value: >= Upper } => 1,
        //~ { Value: 9007199254740993L }
    };
}
