//@ should_fail
// Ensure that SPIRE016 IS triggered when a switch expression arm casts a variable to StatusNoZero.
public class Detect_CastToEnum_Variable_SwitchExpressionArm
{
    public StatusNoZero Method(string tag, int rawValue)
    {
        return tag switch
        {
            "active" => StatusNoZero.Active,
            _ => (StatusNoZero)rawValue, //~ ERROR
        };
    }
}
