//@ should_pass
// Ensure that SPIRE015 is NOT triggered when a switch expression on Color? has arms for Red, Green, and Blue.
public class NoReport_SwitchExpression_NullableEnum_AllMembersCovered
{
    public string Method(Color? value)
    {
        return value switch
        {
            Color.Red => "red",
            Color.Green => "green",
            Color.Blue => "blue",
            _ => "null",
        };
    }
}
