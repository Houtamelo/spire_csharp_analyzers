//@ not_exhaustive
// Correlated numeric and bool columns leave one low-false and one high-true region.
// The long bounds are shifted above 2^53 to keep the numeric split exact.
public class LongBoolTupleCorrelationGap
{
    public int Test((long Value, bool Flag) value) => value switch
    {
        (<= 9007199254740997L, true) => 1,
        (>= 9007199254740995L, false) => 2,
        //~ (<= 9007199254740994L, false)
        //~ (>= 9007199254740998L, true)
    };
}
