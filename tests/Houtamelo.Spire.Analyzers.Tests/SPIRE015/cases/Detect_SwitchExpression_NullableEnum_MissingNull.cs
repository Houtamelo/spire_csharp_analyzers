//@ should_fail
// All enum members covered on Color? but no null arm and no discard — SPIRE015
public class Detect_SwitchExpression_NullableEnum_MissingNull
{
    public string Method(Color? value)
    {
        return value switch //~ ERROR
        {
            Color.Red => "red",
            Color.Green => "green",
            Color.Blue => "blue",
        };
    }
}
