//@ should_pass
// Ensure that SPIRE015 is NOT triggered when Color.Red and not Color.Red together cover all members.
public class NoReport_SwitchExpression_NotPattern_CoversRemainder
{
    public string Method(Color color)
    {
        return color switch
        {
            Color.Red => "red",
            not Color.Red => "not red",
        };
    }
}
