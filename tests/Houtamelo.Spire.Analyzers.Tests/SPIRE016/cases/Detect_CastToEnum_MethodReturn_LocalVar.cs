//@ should_fail
// Ensure that SPIRE016 IS triggered when a method return value is cast to StatusNoZero.
public class Detect_CastToEnum_MethodReturn_LocalVar
{
    private int GetValue() => 1;

    public void Method()
    {
        StatusNoZero x = (StatusNoZero)GetValue(); //~ ERROR
    }
}
