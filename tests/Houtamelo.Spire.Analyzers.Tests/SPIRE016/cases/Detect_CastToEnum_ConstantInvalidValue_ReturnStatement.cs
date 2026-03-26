//@ should_fail
// Ensure that SPIRE016 IS triggered when (StatusNoZero)42 is returned from a method.
public class Detect_CastToEnum_ConstantInvalidValue_ReturnStatement
{
    public StatusNoZero Method()
    {
        return (StatusNoZero)42; //~ ERROR
    }
}
