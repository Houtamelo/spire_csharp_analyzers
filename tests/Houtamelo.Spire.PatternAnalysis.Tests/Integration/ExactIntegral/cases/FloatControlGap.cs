//@ not_exhaustive
// The float control still reports the open interval around zero.
public class FloatControlGap
{
    public int Test(float value) => value switch
    {
        < 0.0f => -1,
        > 0.0f => 1,
        //~ 0.0f
    };
}
