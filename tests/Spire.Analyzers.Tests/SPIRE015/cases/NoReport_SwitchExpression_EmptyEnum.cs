//@ should_pass
// Ensure that SPIRE015 is NOT triggered when switching on Empty in a switch expression with only a discard arm.
public class NoReport_SwitchExpression_EmptyEnum
{
    public string Method(Empty value)
    {
        return value switch
        {
            _ => "none",
        };
    }
}
