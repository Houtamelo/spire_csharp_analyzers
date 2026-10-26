//@ not_exhaustive
// Tuple decomposition keeps the exact gap in its first column.
public class LongTupleGap
{
    const long Lower = 9007199254740992L;
    const long Upper = 9007199254740994L;

    public int Test((long Value, byte Tag) value) => value switch
    {
        (<= Lower, _) => 0,
        (>= Upper, _) => 1,
        //~ (9007199254740993L, _)
    };
}
