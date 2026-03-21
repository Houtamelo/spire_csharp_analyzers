//@ should_pass
// Ensure that SPIRE015 is NOT triggered when a single arm Color.Red or Color.Green or Color.Blue covers all three members.
public class NoReport_SwitchExpression_OrPattern_TripleOr
{
    public string Method(Color color)
    {
        return color switch
        {
            Color.Red or Color.Green or Color.Blue => "all",
        };
    }
}
