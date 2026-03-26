//@ should_fail
// Ensure that SPIRE016 IS triggered when a method parameter integer is cast to StatusNoZero and returned.
public class Detect_CastToEnum_Variable_ReturnStatement
{
    public StatusNoZero Method(int intParam)
    {
        return (StatusNoZero)intParam; //~ ERROR
    }
}
