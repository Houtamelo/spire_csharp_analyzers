//@ should_fail
// Ensure that SPIRE015 IS triggered when neither the 0-value group nor Second are handled (only discard arm).
public class Detect_SwitchExpression_AliasedEnum_AllMissingExceptAlias
{
    public int Method(AliasedEnum value)
    {
        return value switch //~ ERROR
        {
            _ => 0,
        };
    }
}
