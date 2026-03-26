//@ should_pass
// Ensure that SPIRE015 is NOT triggered when a switch expression on PlainEnum has only one arm and a discard.
public class NoReport_SwitchExpression_PlainEnum_OnlyOneArm
{
    public string Method(PlainEnum value)
    {
        return value switch
        {
            PlainEnum.A => "A",
            _ => "other",
        };
    }
}
