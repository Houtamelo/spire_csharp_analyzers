//@ should_fail
// Ensure that SPIRE016 IS triggered when a local variable integer is cast to StatusNoZero.
public class Detect_CastToEnum_Variable_LocalVar
{
    public void Method()
    {
        int someInt = 1;
        StatusNoZero x = (StatusNoZero)someInt; //~ ERROR
    }
}
