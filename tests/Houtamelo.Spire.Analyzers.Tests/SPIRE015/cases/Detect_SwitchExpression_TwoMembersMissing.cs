//@ should_fail
// Ensure that SPIRE015 IS triggered when a switch expression covers only Red, leaving Green and Blue uncovered.
public class Detect_SwitchExpression_TwoMembersMissing
{
    public string Method(Color color)
    {
        return color switch //~ ERROR
        {
            Color.Red => "red",
            _ => "other",
        };
    }
}
