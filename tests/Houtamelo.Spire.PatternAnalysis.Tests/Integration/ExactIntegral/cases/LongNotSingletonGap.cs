//@ not_exhaustive
// A not-pattern leaves exactly the high-value singleton uncovered.
public class LongNotSingletonGap
{
    public int Test(long value) => value switch
    {
        not 9007199254740993L => 0,
        //~ 9007199254740993L
    };
}
