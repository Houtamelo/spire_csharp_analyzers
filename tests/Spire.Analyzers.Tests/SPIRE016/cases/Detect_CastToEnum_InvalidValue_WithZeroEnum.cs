//@ should_fail
// Ensure that SPIRE016 IS triggered when (StatusWithZero)99 is cast, 99 not a named member even though 0 is.
public class Detect_CastToEnum_InvalidValue_WithZeroEnum
{
    public void Method()
    {
        StatusWithZero x = (StatusWithZero)99; //~ ERROR
    }
}
