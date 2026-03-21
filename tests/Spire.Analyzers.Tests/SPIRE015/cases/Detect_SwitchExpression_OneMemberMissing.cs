//@ should_fail
// Ensure that SPIRE015 IS triggered when a switch expression covers Red and Green but not Blue of Color.
public class Detect_SwitchExpression_OneMemberMissing
{
    public string Method(Color color)
    {
        return color switch //~ ERROR
        {
            Color.Red => "red",
            Color.Green => "green",
            _ => "other",
        };
    }
}
