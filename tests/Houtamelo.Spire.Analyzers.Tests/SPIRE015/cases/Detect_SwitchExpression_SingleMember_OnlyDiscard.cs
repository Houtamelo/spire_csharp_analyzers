//@ should_fail
// Ensure that SPIRE015 IS triggered when a switch expression on SingleMember has only a discard arm.
public class Detect_SwitchExpression_SingleMember_OnlyDiscard
{
    public string Method(SingleMember value)
    {
        return value switch //~ ERROR
        {
            _ => "other",
        };
    }
}
