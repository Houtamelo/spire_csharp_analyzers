//@ should_pass
// Ensure that SPIRE015 is NOT triggered when a switch expression covers all three members of Color.
public class NoReport_SwitchExpression_AllMembersCovered
{
    public string Method(Color color)
    {
        return color switch
        {
            Color.Red => "red",
            Color.Green => "green",
            Color.Blue => "blue",
        };
    }
}
