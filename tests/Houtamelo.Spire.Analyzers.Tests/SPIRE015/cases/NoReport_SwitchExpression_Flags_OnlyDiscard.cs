//@ should_pass
// SPIRE015 does NOT fire on a [Flags] enum when the sole arm is a discard — the catch-all opts out of the check.
public class NoReport_SwitchExpression_Flags_OnlyDiscard
{
    public string Method(Permission permission)
    {
        return permission switch
        {
            _ => "other",
        };
    }
}
