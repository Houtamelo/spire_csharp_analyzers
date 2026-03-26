//@ should_fail
// Ensure that SPIRE015 IS triggered when an or-pattern covers Red and Green but Blue is missing.
public class Detect_SwitchExpression_OrPattern_OneMemberMissing
{
    public string Method(Color color)
    {
        return color switch //~ ERROR
        {
            Color.Red or Color.Green => "not blue",
        };
    }
}
