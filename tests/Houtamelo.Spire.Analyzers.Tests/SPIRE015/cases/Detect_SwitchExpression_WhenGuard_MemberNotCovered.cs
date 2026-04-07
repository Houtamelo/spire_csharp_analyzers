//@ should_fail
// Ensure that SPIRE015 IS triggered when the Red arm has a when guard — Red is not considered covered, Green and Blue are present without guards.
public class Detect_SwitchExpression_WhenGuard_MemberNotCovered
{
    public string Method(Color color, bool condition)
    {
        return color switch //~ ERROR
        {
            Color.Red when condition => "red guarded",
            Color.Green => "green",
            Color.Blue => "blue",
        };
    }
}
