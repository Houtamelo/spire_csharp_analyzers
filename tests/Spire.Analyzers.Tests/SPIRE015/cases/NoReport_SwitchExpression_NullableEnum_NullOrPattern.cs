//@ should_pass
// All enum members covered, Blue and null combined via or-pattern — no diagnostic
public class NoReport_SwitchExpression_NullableEnum_NullOrPattern
{
    public string Method(Color? value)
    {
        return value switch
        {
            Color.Red => "red",
            Color.Green => "green",
            null or Color.Blue => "blue-or-null",
        };
    }
}
