//@ should_pass
// Ensure that SPIRE015 is NOT triggered when SingleMember.Only => ... is the sole arm in a switch expression.
public class NoReport_SwitchExpression_SingleMember_Covered
{
    public string Method(SingleMember value)
    {
        return value switch
        {
            SingleMember.Only => "only",
        };
    }
}
