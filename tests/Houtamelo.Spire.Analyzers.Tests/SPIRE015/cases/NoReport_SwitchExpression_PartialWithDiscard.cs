//@ should_pass
// SPIRE015 does NOT fire when an arm covers Red and _ => ... handles the rest — the discard catch-all opts out.
public class NoReport_SwitchExpression_PartialWithDiscard
{
    public string Method(Color color)
    {
        return color switch
        {
            Color.Red => "red",
            _ => "other",
        };
    }
}
