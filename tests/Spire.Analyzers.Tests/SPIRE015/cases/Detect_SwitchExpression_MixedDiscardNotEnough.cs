//@ should_fail
// Ensure that SPIRE015 IS triggered when arms cover Red only and _ => ... handles the rest — Green and Blue remain uncovered.
public class Detect_SwitchExpression_MixedDiscardNotEnough
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
