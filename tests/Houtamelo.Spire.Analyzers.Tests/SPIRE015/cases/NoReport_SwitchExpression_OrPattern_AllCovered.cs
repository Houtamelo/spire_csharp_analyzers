//@ should_pass
// Ensure that SPIRE015 is NOT triggered when Color.Red or Color.Green combined with Color.Blue covers all members.
public class NoReport_SwitchExpression_OrPattern_AllCovered
{
    public string Method(Color color)
    {
        return color switch
        {
            Color.Red or Color.Green => "not blue",
            Color.Blue => "blue",
        };
    }
}
