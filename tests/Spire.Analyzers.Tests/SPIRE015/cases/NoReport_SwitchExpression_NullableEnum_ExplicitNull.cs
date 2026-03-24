//@ should_pass
// All enum members + explicit null arm on Color? — no diagnostic
public class NoReport_SwitchExpression_NullableEnum_ExplicitNull
{
    public string Method(Color? value)
    {
        return value switch
        {
            null => "none",
            Color.Red => "red",
            Color.Green => "green",
            Color.Blue => "blue",
        };
    }
}
