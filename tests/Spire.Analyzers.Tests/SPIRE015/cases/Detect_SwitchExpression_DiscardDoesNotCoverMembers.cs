//@ should_fail
// Ensure that SPIRE015 IS triggered when the only arm is _ => ... on Color (all three members uncovered).
public class Detect_SwitchExpression_DiscardDoesNotCoverMembers
{
    public string Method(Color color)
    {
        return color switch //~ ERROR
        {
            _ => "other",
        };
    }
}
