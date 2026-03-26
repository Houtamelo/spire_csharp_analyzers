//@ should_fail
// Ensure that SPIRE015 IS triggered when a switch expression has only a discard arm _ => ... on Color.
public class Detect_SwitchExpression_AllMembersMissing
{
    public string Method(Color color)
    {
        return color switch //~ ERROR
        {
            _ => "other",
        };
    }
}
