//@ should_fail
// Ensure that SPIRE016 IS triggered when (StatusNoZero)(-1) is assigned to a local variable.
public class Detect_CastToEnum_NegativeValue_LocalVar
{
    public void Method()
    {
        StatusNoZero x = (StatusNoZero)(-1); //~ ERROR
    }
}
