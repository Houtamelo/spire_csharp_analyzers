//@ should_fail
// Ensure that SPIRE016 IS triggered when a switch arm yields (StatusNoZero)0.
public class Detect_CastToEnum_ConstantInvalidValue_SwitchArm
{
    public StatusNoZero Method(int input)
    {
        return input switch
        {
            1 => StatusNoZero.Active,
            _ => (StatusNoZero)0, //~ ERROR
        };
    }
}
