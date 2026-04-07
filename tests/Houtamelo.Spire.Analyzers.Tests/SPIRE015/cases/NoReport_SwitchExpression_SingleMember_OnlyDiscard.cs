//@ should_pass
// SPIRE015 does NOT fire on a single-member enum when the sole arm is a discard — the catch-all opts out.
public class NoReport_SwitchExpression_SingleMember_OnlyDiscard
{
    public string Method(SingleMember value)
    {
        return value switch
        {
            _ => "other",
        };
    }
}
