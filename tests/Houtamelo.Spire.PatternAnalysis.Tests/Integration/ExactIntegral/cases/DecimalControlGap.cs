//@ not_exhaustive
// Decimal zero remains the only missing value in this control case.
public class DecimalControlGap
{
    public int Test(decimal value) => value switch
    {
        < 0m => -1,
        > 0m => 1,
        //~ 0m
    };
}
