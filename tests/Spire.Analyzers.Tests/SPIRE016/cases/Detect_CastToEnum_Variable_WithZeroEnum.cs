//@ should_fail
// Ensure that SPIRE016 IS triggered when a variable is cast to StatusWithZero — unknown value, zero member doesn't help.
public class Detect_CastToEnum_Variable_WithZeroEnum
{
    public void Method(int someVar)
    {
        StatusWithZero x = (StatusWithZero)someVar; //~ ERROR
    }
}
