//@ not_exhaustive
// Gap at exactly 0: > 0 and < 100 skips 0, >= 100 covers high, < 0 covers negative
public class AndPattern_NumericGap
{
    public int Test(int x) => x switch
    {
        > 0 and < 100 => 1,
        >= 100 => 2,
        < 0 => 3,
    };
}
