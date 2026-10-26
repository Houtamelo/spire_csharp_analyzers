//@ not_exhaustive
// These literal bounds leave 9007199254740993, which is not representable exactly as a double.
public class LongAboveDoublePrecisionGap
{
    public int Test(long value) => value switch
    {
        <= 9007199254740992L => 0,
        >= 9007199254740994L => 1,
        //~ 9007199254740993L
    };
}
