//@ not_exhaustive
// These literal bounds leave 9007199254740993UL, which is not representable exactly as a double.
public class ULongAboveDoublePrecisionGap
{
    public int Test(ulong value) => value switch
    {
        <= 9007199254740992UL => 0,
        >= 9007199254740994UL => 1,
        //~ 9007199254740993UL
    };
}
