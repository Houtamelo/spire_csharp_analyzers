//@ should_fail
// Ensure that SPIRE016 IS triggered when a lambda captures a parameter and casts it to StatusNoZero.
public class Detect_CastToEnum_Variable_LambdaBody
{
    public void Method(int intParam)
    {
        Func<StatusNoZero> fn = () => (StatusNoZero)intParam; //~ ERROR
    }
}
