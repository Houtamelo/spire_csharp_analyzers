//@ should_fail
// Ensure that SPIRE016 IS triggered when a static field is initialized with (StatusNoZero)GetRawValue().
public class Detect_CastToEnum_Variable_StaticFieldInit
{
    private static int GetRawValue() => 1;

    public static StatusNoZero Value = (StatusNoZero)GetRawValue(); //~ ERROR
}
