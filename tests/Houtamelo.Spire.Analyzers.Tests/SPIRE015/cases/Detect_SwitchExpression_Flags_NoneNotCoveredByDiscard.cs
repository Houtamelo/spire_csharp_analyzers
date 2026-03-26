//@ should_fail
// Ensure that SPIRE015 IS triggered when only a discard arm is present — None and all other members are missing.
public class Detect_SwitchExpression_Flags_NoneNotCoveredByDiscard
{
    public string Method(Permission permission)
    {
        return permission switch //~ ERROR
        {
            _ => "other",
        };
    }
}
