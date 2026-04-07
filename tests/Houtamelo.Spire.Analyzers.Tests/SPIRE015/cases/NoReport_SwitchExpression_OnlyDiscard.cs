//@ should_pass
// SPIRE015 does NOT fire when the only arm is _ => ... — the discard catch-all opts out of the check.
public class NoReport_SwitchExpression_OnlyDiscard
{
    public string Method(Color color)
    {
        return color switch
        {
            _ => "other",
        };
    }
}
