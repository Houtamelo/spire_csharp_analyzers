//@ should_pass
// SPIRE015 does NOT fire on an aliased enum when the sole arm is a discard — the catch-all opts out of the check.
public class NoReport_SwitchExpression_AliasedEnum_OnlyDiscard
{
    public int Method(AliasedEnum value)
    {
        return value switch
        {
            _ => 0,
        };
    }
}
