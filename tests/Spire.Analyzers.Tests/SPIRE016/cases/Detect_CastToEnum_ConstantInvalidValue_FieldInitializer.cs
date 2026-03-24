//@ should_fail
// Ensure that SPIRE016 IS triggered when a field is initialized with (StatusNoZero)100.
public class Detect_CastToEnum_ConstantInvalidValue_FieldInitializer
{
    public StatusNoZero Value = (StatusNoZero)100; //~ ERROR
}
