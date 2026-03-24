//@ should_fail
// Ensure that SPIRE016 IS triggered when a lambda body returns (StatusNoZero)0.
public class Detect_CastToEnum_ConstantInvalidValue_LambdaBody
{
    public void Method()
    {
        Func<StatusNoZero> fn = () => (StatusNoZero)0; //~ ERROR
    }
}
